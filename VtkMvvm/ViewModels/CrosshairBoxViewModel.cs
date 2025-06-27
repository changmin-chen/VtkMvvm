using Kitware.VTK;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels;

/// <summary>
/// A 2-D cross-hair that works in any slicing plane.
/// Supply the two orthonormal in-plane directions (u,v) once,
/// then update FocalPoint whenever the user changes the pick.
/// </summary>
public sealed class CrosshairBoxViewModel : VtkElementViewModel
{
    // ── geometry helpers ───────────────────────────────────────────
    private readonly double[] _half;        // hx, hy, hz
    private Double3 _u;                     // first in-plane axis (unit)
    private Double3 _v;                     // second in-plane axis (unit)

    private readonly vtkLineSource   _lineU = vtkLineSource.New();
    private readonly vtkLineSource   _lineV = vtkLineSource.New();
    private readonly vtkAppendPolyData _append = vtkAppendPolyData.New();
    private readonly vtkPolyDataMapper _mapper = vtkPolyDataMapper.New();

    private Double3 _focalPoint;

    // ── ctor ───────────────────────────────────────────────────────
    public CrosshairBoxViewModel(
        Double3           uDir,             // plane X-axis  (unit)
        Double3           vDir,             // plane Y-axis  (unit)
        double[]          imageBounds)      // xmin,xmax, ymin,ymax, zmin,zmax
    {
        if (imageBounds.Length != 6) throw new ArgumentException(nameof(imageBounds));

        _u = uDir.Normalized();
        _v = vDir.Normalized();

        _half = new[]
        {
            0.5 * (imageBounds[1] - imageBounds[0]),   // hx
            0.5 * (imageBounds[3] - imageBounds[2]),   // hy
            0.5 * (imageBounds[5] - imageBounds[4])    // hz
        };

        // VTK plumbing: U-line + V-line → append → mapper → actor
        _append.AddInputConnection(_lineU.GetOutputPort());
        _append.AddInputConnection(_lineV.GetOutputPort());
        _mapper.SetInputConnection(_append.GetOutputPort());

        vtkActor act = vtkActor.New();
        act.SetMapper(_mapper);
        act.GetProperty().SetColor(1, 0, 0);   // red
        act.GetProperty().SetLineWidth(1.5f);
        Actor = act;
    }

    public override vtkActor Actor { get; }

    // ── Bindable properties ────────────────────────────────────────
    public Double3 FocalPoint
    {
        get => _focalPoint;
        set
        {
            if (SetField(ref _focalPoint, value))
            {
                RebuildLines();
                _append.Modified();
                OnModified();
            }
        }
    }

    /// <summary>
    /// Change the plane orientation on-the-fly (e.g. user rotates oblique view).
    /// Provide the *new* orthonormal basis.
    /// </summary>
    public void UpdatePlaneAxes(Double3 uDir, Double3 vDir)
    {
        _u = uDir.Normalized();
        _v = vDir.Normalized();
        RebuildLines();
        _append.Modified();
        OnModified();
    }

    // ── private helpers ────────────────────────────────────────────
    private void RebuildLines()
    {
        (double fx, double fy, double fz) = _focalPoint;

        // helper lambda
        void SetLine(vtkLineSource ls, Double3 d)
        {
            double h = Math.Abs(d.X) * _half[0] +
                       Math.Abs(d.Y) * _half[1] +
                       Math.Abs(d.Z) * _half[2];

            ls.SetPoint1(fx - d.X * h, fy - d.Y * h, fz - d.Z * h);
            ls.SetPoint2(fx + d.X * h, fy + d.Y * h, fz + d.Z * h);
            ls.Modified();
        }

        SetLine(_lineU, _u);
        SetLine(_lineV, _v);
    }
}