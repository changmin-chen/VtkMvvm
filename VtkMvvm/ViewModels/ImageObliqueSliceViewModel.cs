using System.Numerics;
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
public sealed class ImageObliqueSliceViewModel : VtkElementViewModel, ISlicePlaneInfo
{
    // ── VTK pipeline ────────────────────────────────────────────────
    private readonly vtkImageReslice _reslice = vtkImageReslice.New();
    private readonly vtkImageMapToColors _cmap = vtkImageMapToColors.New();
    private readonly vtkTransform _xfm = vtkTransform.New();
    private readonly vtkMatrix4x4 _axes = vtkMatrix4x4.New(); // reslice axes
    private readonly vtkImageActor _actor = vtkImageActor.New();

    private readonly double[] _imgCentre;
    private readonly double[] _imgBounds;
    private readonly double[] _spacing;

    // ── cached values for slider & step ─────────────────────────────
    private double _step; // Δ mm per slice index
    private int _minSliceIdx;
    private int _maxSliceIdx;
    private int _sliceIndex = int.MinValue;
    private Quaternion _sliceOrientation = Quaternion.Identity;


    public ImageObliqueSliceViewModel(
        Quaternion orientation,
        ColoredImagePipeline pipeline)
    {
        vtkImageData volume = pipeline.Image;

        ImageModel = ImageModel.Create(volume);
        _imgCentre = volume.GetCenter();
        _imgBounds = volume.GetBounds();
        _spacing = volume.GetSpacing();

        // -------- reslice: 2-D image in its own XY coord system -----
        _reslice.SetInput(volume);
        _reslice.SetInterpolationModeToLinear();
        _reslice.SetOutputDimensionality(2);
        _reslice.AutoCropOutputOn();
        _reslice.SetBackgroundLevel(volume.GetScalarRange()[0]);
        _reslice.SetResliceAxes(_axes); // we will fill it below

        // -------- connect full display pipeline ---------------------
        _actor.SetUserTransform(_xfm); // ***positions slice in 3-D***
        pipeline.ConnectWithReslice(_cmap, _reslice, _actor);
        Actor = _actor;

        // -------- initialise orientation & index --------------------
        SliceOrientation = orientation; // sets step & slider limits
        SliceIndex = 0; // central slice
    }

    // ── public surface identical to the old VM ─────────────────────
    public override vtkImageActor Actor { get; }
    public ImageModel ImageModel { get; }

    public int SliceIndex
    {
        get => _sliceIndex;
        set
        {
            value = Math.Clamp(value, _minSliceIdx, _maxSliceIdx);
            if (!SetField(ref _sliceIndex, value)) return;
            SetSliceIndex(value);
            OnModified();
        }
    }

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

    public int MinSliceIndex => _minSliceIdx;
    public int MaxSliceIndex => _maxSliceIdx;


    // convenience accessors for the plane axes (unchanged API)
    public Vector3 PlaneAxisU { get; private set; }
    public Vector3 PlaneAxisV { get; private set; }
    public Vector3 PlaneNormal { get; private set; }
    public Double3 PlaneOrigin { get; private set; }

    //----------------------------------------------------------------
    /// <summary>Apply a new orientation: updates reslice axes, step, slider range.</summary>
    private void SetOrientation(Quaternion q)
    {
        q = Quaternion.Normalize(q);

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

        // ---- distance per slice index (Δ) --------------------------
        _step = Math.Abs(n.X) * _spacing[0] +
                Math.Abs(n.Y) * _spacing[1] +
                Math.Abs(n.Z) * _spacing[2];

        // ---- slider limits via support-function distance -----------
        double hx = 0.5 * (_imgBounds[1] - _imgBounds[0]);
        double hy = 0.5 * (_imgBounds[3] - _imgBounds[2]);
        double hz = 0.5 * (_imgBounds[5] - _imgBounds[4]);

        double maxDist = Math.Abs(n.X) * hx + Math.Abs(n.Y) * hy + Math.Abs(n.Z) * hz;
        int maxIdx = (int)Math.Floor(maxDist / _step);
        _minSliceIdx = -maxIdx;
        _maxSliceIdx = maxIdx;
        OnPropertyChanged(nameof(MinSliceIndex));
        OnPropertyChanged(nameof(MaxSliceIndex));

        // ----- keep current index inside the new range --------------
        _sliceIndex = Math.Clamp(_sliceIndex, _minSliceIdx, _maxSliceIdx);
        SetSliceIndex(_sliceIndex); // ★ always update transform
    }

    /// <summary>Move the slice plane along its normal by idx · Δ.</summary>
    private void SetSliceIndex(int idx)
    {
        // current normal (3rd column)
        double nx = _axes.GetElement(0, 2);
        double ny = _axes.GetElement(1, 2);
        double nz = _axes.GetElement(2, 2);

        // gives translation from dataset centre
        double ox = _imgCentre[0] + nx * idx * _step;
        double oy = _imgCentre[1] + ny * idx * _step;
        double oz = _imgCentre[2] + nz * idx * _step;
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
        _actor.Modified();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _actor.Dispose();
            _cmap.Dispose();
            _reslice.Dispose();
            _xfm.Dispose();
            _axes.Dispose();
        }
        base.Dispose(disposing);
    }
}