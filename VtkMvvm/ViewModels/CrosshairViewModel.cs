using System.Numerics;
using Kitware.VTK;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels;

/// <summary>
/// A 2-D cross-hair that works in any slicing plane.
/// Supply the two orthonormal in-plane directions (u,v) once,
/// then update FocalPoint whenever the user changes the pick.
/// <remarks>
/// Core idea: halfLength(d) = |d.x|·hx + |d.y|·hy + |d.z|·hz
/// where hx, hy, hz are the half-extents of the volume and d is a unit direction in that plane.
/// L1:  F − u·halfLength(u) ⟶ F + u·halfLength(u)
/// L2:  F − v·halfLength(v) ⟶ F + v·halfLength(v)
/// </remarks>
/// </summary>
public sealed class CrosshairViewModel : VtkElementViewModel
{
    // ── geometry helpers ───────────────────────────────────────────
    private readonly double[] _half; // hx, hy, hz for line boundary
    private Vector3 _u; // first in-plane axis (unit)
    private Vector3 _v; // second in-plane axis (unit)

    private readonly vtkLineSource _lineU = vtkLineSource.New();
    private readonly vtkLineSource _lineV = vtkLineSource.New();
    private readonly vtkAppendPolyData _append = vtkAppendPolyData.New();
    private readonly vtkPolyDataMapper _mapper = vtkPolyDataMapper.New();

    private Double3 _focalPoint;

    private CrosshairViewModel(
        Vector3 uDir, // plane X-axis  (unit)
        Vector3 vDir, // plane Y-axis  (unit)
        double[] imageBounds) // xmin,xmax, ymin,ymax, zmin,zmax
    {
        if (imageBounds.Length != 6) throw new ArgumentException(nameof(imageBounds));

        _u = Vector3.Normalize(uDir);
        _v = Vector3.Normalize(vDir);

        _half =
        [
            0.5 * (imageBounds[1] - imageBounds[0]), // hx
            0.5 * (imageBounds[3] - imageBounds[2]), // hy
            0.5 * (imageBounds[5] - imageBounds[4]) // hz
        ];

        // VTK plumbing: U-line + V-line → append → mapper → actor
        _append.AddInputConnection(_lineU.GetOutputPort());
        _append.AddInputConnection(_lineV.GetOutputPort());
        _mapper.SetInputConnection(_append.GetOutputPort());

        vtkActor act = vtkActor.New();
        act.SetMapper(_mapper);
        act.GetProperty().SetColor(1, 0, 0); // red
        act.GetProperty().SetLineWidth(1.5f);
        Actor = act;
    }

    public static CrosshairViewModel Create(SliceOrientation orientation, double[] imageBounds)
    {
        return orientation switch
        {
            SliceOrientation.Axial => new CrosshairViewModel(Vector3.UnitX, Vector3.UnitY, imageBounds),
            SliceOrientation.Coronal => new CrosshairViewModel(Vector3.UnitX, Vector3.UnitZ, imageBounds),
            SliceOrientation.Sagittal => new CrosshairViewModel(Vector3.UnitY, Vector3.UnitZ, imageBounds),
            _ => throw new ArgumentOutOfRangeException(nameof(orientation))
        };
    }

    public override vtkActor Actor { get; }

    // ── Bindable properties ────────────────────────────────────────
    public Double3 FocalPoint
    {
        get => _focalPoint;
        set
        {
            if (!SetField(ref _focalPoint, value)) return;
            RebuildLines();
            _append.Modified();
            OnModified();
        }
    }

    /// <summary>
    /// Change the plane orientation on-the-fly (e.g. user rotates oblique view).
    /// Provide the *new* orthonormal basis.
    /// </summary>
    public void UpdatePlaneAxes(Vector3 uDir, Vector3 vDir)
    {
        _u = Vector3.Normalize(uDir);
        _v = Vector3.Normalize(vDir);
        RebuildLines();
        _append.Modified();
        OnModified();
    }

    // ── private helpers ────────────────────────────────────────────
    private void RebuildLines()
    {
        (double fx, double fy, double fz) = _focalPoint;

        // helper lambda
        void SetLine(vtkLineSource ls, Vector3 vec)
        {
            double h = Math.Abs(vec.X) * _half[0] + Math.Abs(vec.Y) * _half[1] + Math.Abs(vec.Z) * _half[2];

            ls.SetPoint1(fx - vec.X * h, fy - vec.Y * h, fz - vec.Z * h);
            ls.SetPoint2(fx + vec.X * h, fy + vec.Y * h, fz + vec.Z * h);
            ls.Modified();
        }

        SetLine(_lineU, _u);
        SetLine(_lineV, _v);
    }
}