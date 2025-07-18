﻿using System.Numerics;
using Kitware.VTK;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels.Base;

namespace VtkMvvm.ViewModels;

/// <summary>
/// Oblique slice that really sits in 3-D.  Works with ActiViz/VTK 5.8
/// (vtkImageActor + vtkTransform) and keeps the public surface identical
/// to the original implementation so existing bindings continue to work.
/// </summary>
public sealed class ImageObliqueSliceViewModel : ImageSliceViewModel
{
    // ── VTK pipeline ────────────────────────────────────────────────
    private readonly vtkMatrix4x4 _axes = vtkMatrix4x4.New(); // reslice axes
    private readonly vtkTransform _xfm = vtkTransform.New();
    private readonly vtkImageReslice _reslice = vtkImageReslice.New();

    private readonly double[] _imgCentre;
    private readonly Bounds _imgBounds;
    private readonly double[] _spacing;

    // ── cached values for slider & step ─────────────────────────────
    private int _minSliceIdx;
    private int _maxSliceIdx;
    private Quaternion _sliceOrientation;


    public ImageObliqueSliceViewModel(Quaternion orientation, ColoredImagePipeline pipeline) : base(pipeline)
    {
        vtkImageData volume = pipeline.Image;
        _imgCentre = volume.GetCenter();
        _imgBounds = Bounds.FromArray(volume.GetBounds());
        _spacing = volume.GetSpacing();
        
        // -------- connect full display pipeline ---------------------
        var slicePort = BuildObliqueSlice(volume);
        ColorMap.SetInputConnection(slicePort);
        Actor.SetInput(ColorMap.GetOutput());
        Actor.SetUserTransform(_xfm); // positions slice in 3-D

        // -------- initialise orientation & index --------------------
        _sliceOrientation = orientation; // sets step & slider limits
        SetOrientation(orientation); 
        SliceIndex = 0; // central slice
    }

    private vtkAlgorithmOutput BuildObliqueSlice(vtkImageData image)
    {
        // -------- reslice: 2-D image in its own XY coord system -----
        _reslice.SetInput(image);
        _reslice.SetInterpolationModeToLinear();
        _reslice.SetOutputDimensionality(2);
        _reslice.AutoCropOutputOn();
        _reslice.SetBackgroundLevel(image.GetScalarRange()[0]);
        _reslice.SetResliceAxes(_axes); // we will fill it below
        
        return _reslice.GetOutputPort();
    }

    // ── public surface  ─────────────────────

    /// <summary>
    /// Δ mm per slice index
    /// </summary>
    public double StepMillimeter { get; private set; }
    
    public Quaternion SliceOrientation
    {
        get => _sliceOrientation;
        set
        {
            if (!SetField(ref _sliceOrientation, value)) return;
            SetOrientation(value);
            OnModified();
        }
    }

    public int MinSliceIndex
    {
        get => _minSliceIdx;
        private set => SetField(ref _minSliceIdx, value);
    }

    public int MaxSliceIndex
    {
        get => _maxSliceIdx;
        private set => SetField(ref _maxSliceIdx, value);
    }

    /// <summary>
    /// Convert a world coordinate to the oblique slice stack:
    ///   – <paramref name="idx"/>  : slice index along the normal
    ///   – (<paramref name="i"/>,<paramref name="j"/>): in-plane pixel coords
    /// Returns false if the point lies outside the volume.
    /// </summary>
    public bool TryWorldToSlice(Double3 w, out int idx, out double i, out double j)
    {
        // ---------- 1. signed distance along the rail -----------------
        Vector3 d = new((float)(w.X - _imgCentre[0]),
            (float)(w.Y - _imgCentre[1]),
            (float)(w.Z - _imgCentre[2]));

        double dist = Vector3.Dot(d, PlaneNormal); // mm
        idx = (int)Math.Round(dist / StepMillimeter); // your new slice index

        if (idx < _minSliceIdx || idx > _maxSliceIdx)
        {
            i = j = double.NaN; // outside dataset
            return false;
        }

        // ---------- 2. where is the origin of that slice? -------------
        Vector3 origin = new Vector3((float)_imgCentre[0],
                             (float)_imgCentre[1],
                             (float)_imgCentre[2])
                         + PlaneNormal * (float)(idx * StepMillimeter);

        // ---------- 3. in-plane pixel coordinates ---------------------
        Vector3 rel = new((float)(w.X - origin.X),
            (float)(w.Y - origin.Y),
            (float)(w.Z - origin.Z));

        // PlaneAxisU/V already hold unit vectors; divide by voxel pitch
        i = Vector3.Dot(rel, PlaneAxisU) / _spacing[0]; 
        j = Vector3.Dot(rel, PlaneAxisV) / _spacing[1]; 
        return true;
    }


    //----------------------------------------------------------------
    /// <summary>Apply a new orientation: updates reslice axes, step, slider range.</summary>
    private void SetOrientation(Quaternion q)
    {
        q = Quaternion.Normalize(q);
        _axes.Identity();

        // ----- raw orthonormal frame ---------------------------------
        PlaneAxisU = Vector3.Transform(Vector3.UnitX, q); // +slice X (u)
        PlaneAxisV = Vector3.Transform(Vector3.UnitY, q); // +slice Y (v / view-up)
        PlaneNormal = Vector3.Transform(Vector3.UnitZ, q); // +slice Z (normal)

        // -- scaled copy for reslice axes (cols = u v n) --------------
        Vector3 u = PlaneAxisU * (float)_spacing[0];
        Vector3 v = PlaneAxisV * (float)_spacing[1];
        Vector3 n = PlaneNormal; // n needn’t be scaled

        // ---- rotation part of 4×4 (columns = u v n) ----------------
        for (int i = 0; i < 3; ++i)
        {
            _axes.SetElement(i, 0, u[i]);
            _axes.SetElement(i, 1, v[i]);
            _axes.SetElement(i, 2, n[i]);
        }

        // ---- distance per slice index (Δ) euler projection --------------------------
        double dx = n.X / _spacing[0];
        double dy = n.Y / _spacing[1];
        double dz = n.Z / _spacing[2];
        StepMillimeter = 1.0 / Math.Sqrt(dx * dx + dy * dy + dz * dz);

        // ---- slider limits via support-function distance -----------
        double hx = 0.5 * _imgBounds.Width;
        double hy = 0.5 * _imgBounds.Height;
        double hz = 0.5 * _imgBounds.Depth;

        double maxDist = Math.Abs(n.X) * hx + Math.Abs(n.Y) * hy + Math.Abs(n.Z) * hz; // dot product of n with half-extent vector
        int maxIdx = (int)Math.Floor(maxDist / StepMillimeter);
        MinSliceIndex = -maxIdx;
        MaxSliceIndex = maxIdx;

        // ----- keep current index inside the new range --------------
        SliceIndex = Math.Clamp(SliceIndex, _minSliceIdx, _maxSliceIdx);
        OnSliceIndexChanged(SliceIndex); // ★ always update slice idx to fit the current transform
    }

    /// <summary>Move the slice plane along its normal by idx · Δ.</summary>
    protected override void OnSliceIndexChanged(int idx)
    {
        // current normal (3rd column)
        double nx = _axes.GetElement(0, 2);
        double ny = _axes.GetElement(1, 2);
        double nz = _axes.GetElement(2, 2);

        // gives translation from dataset centre
        double ox = _imgCentre[0] + nx * idx * StepMillimeter;
        double oy = _imgCentre[1] + ny * idx * StepMillimeter;
        double oz = _imgCentre[2] + nz * idx * StepMillimeter;
        _axes.SetElement(0, 3, ox);
        _axes.SetElement(1, 3, oy);
        _axes.SetElement(2, 3, oz);
        _axes.SetElement(3, 3, 1);

        // keep a copy of the current origin to callers
        PlaneOrigin = new Double3(ox, oy, oz);
        OnPropertyChanged(nameof(PlaneOrigin));

        // apply to reslice and to actor transform (so the slice shows up in 3-D)
        _reslice.SetResliceAxes(_axes);
        _reslice.Modified();

        _xfm.SetMatrix(_axes); // same 4×4 = slice → world
        _xfm.Modified();
        Actor.Modified();
    }


    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _reslice.Dispose();
            _xfm.Dispose();
            _axes.Dispose();
        }
        base.Dispose(disposing);
    }
}