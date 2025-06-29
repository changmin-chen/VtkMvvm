using Kitware.VTK;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels;

/// <summary>
/// A 2-D cross-hair that works in any slicing plane.
/// Supply the two orthonormal in-plane directions (u,v) once,
/// then update FocalPoint whenever the user changes the pick.
/// </summary>
public sealed class CrosshairViewModel : VtkElementViewModel
{
    // ── geometry helpers ───────────────────────────────────────────
    private Bounds _bounds; // xmin, xmax, ymin, ymax, zmin, zmax
    private Double3 _u; // first in-plane axis (unit)
    private Double3 _v; // second in-plane axis (unit)

    private readonly vtkLineSource _lineU = vtkLineSource.New();
    private readonly vtkLineSource _lineV = vtkLineSource.New();
    private readonly vtkAppendPolyData _append = vtkAppendPolyData.New();
    private readonly vtkPolyDataMapper _mapper = vtkPolyDataMapper.New();

    private Double3 _focalPoint;
    private float _lineWidth = 1.5F;


    private CrosshairViewModel(
        Double3 uDir,
        Double3 vDir,
        Bounds lineBounds)
    {
        _bounds = lineBounds;

        _u = uDir.Normalized();
        _v = vDir.Normalized();

        // VTK plumbing: U-line + V-line → append → mapper → actor
        _append.AddInputConnection(_lineU.GetOutputPort());
        _append.AddInputConnection(_lineV.GetOutputPort());
        _mapper.SetInputConnection(_append.GetOutputPort());

        vtkActor act = vtkActor.New();
        act.SetMapper(_mapper);
        act.GetProperty().SetColor(1, 0, 0); // red
        act.GetProperty().SetLineWidth(_lineWidth);
        Actor = act;

        // Initialize the focal point to bounds center
        FocalPoint = lineBounds.Center;
    }

    /// <summary>
    /// Represents a ViewModel for rendering a crosshair in a 3D world space.
    /// This factory method simplifies creation by using a predefined SliceOrientation
    /// to determine the U and V direction vectors.
    /// </summary>
    public static CrosshairViewModel Create(SliceOrientation orientation, Bounds lineBounds)
    {
        return orientation switch
        {
            SliceOrientation.Axial => new CrosshairViewModel(Double3.UnitX, Double3.UnitY, lineBounds),
            SliceOrientation.Sagittal => new CrosshairViewModel(Double3.UnitY, Double3.UnitZ, lineBounds),
            SliceOrientation.Coronal => new CrosshairViewModel(Double3.UnitX, Double3.UnitZ, lineBounds),
            _ => throw new ArgumentOutOfRangeException(nameof(orientation))
        };
    }

    /// <summary>
    /// Represents a ViewModel for rendering a crosshair in a 3D world space.
    /// This class manages the orientation and positioning of the crosshair lines,
    /// along with the image slice horizontal and vertical in-plane directions. 
    /// </summary>
    /// <param name="uDir">Plane X-axis (unit)</param>
    /// <param name="vDir">Plane Y-axis (unit)</param>
    /// <param name="lineBounds">Boundary of the crosshair lines. xmin,xmax, ymin,ymax, zmin,zmax</param>
    public static CrosshairViewModel Create(Double3 uDir, Double3 vDir, Bounds lineBounds) => new(uDir, vDir, lineBounds);

    public override vtkActor Actor { get; }

    // ── Bindable properties ────────────────────────────────────────
    public Double3 FocalPoint
    {
        get => _focalPoint;
        set
        {
            if (!SetField(ref _focalPoint, value)) return;
            RebuildLines();
            OnModified();
        }
    }

    public float LineWidth
    {
        get => _lineWidth;
        set
        {
            if (!SetField(ref _lineWidth, value)) return;
            Actor.GetProperty().SetLineWidth(value);
            Actor.Modified();
            OnModified();
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
        OnModified();
    }

    public void UpdateBounds(Bounds lineBounds)
    {
        _bounds = lineBounds;
        RebuildLines();
        OnModified();
    }

    // ── private helpers ────────────────────────────────────────────
    private void RebuildLines()
    {
        SetLineToBox(_lineU, _u, _focalPoint, _bounds);
        SetLineToBox(_lineV, _v, _focalPoint, _bounds);
    }

    private static void SetLineToBox(vtkLineSource ls,
        in Double3 dir,
        in Double3 focal,
        Bounds bounds /* xmin,xmax,ymin,ymax,zmin,zmax */)
    {
        double fx = focal.X, fy = focal.Y, fz = focal.Z;
        double dx = dir.X, dy = dir.Y, dz = dir.Z;

        const double eps = 1e-9;
        double tMin = double.NegativeInfinity;
        double tMax = double.PositiveInfinity;

        void Slab(double p, double d, double minB, double maxB)
        {
            if (Math.Abs(d) < eps) // line is parallel to this slab
            {
                if (p < minB || p > maxB) // but outside → no hit
                {
                    tMin = 1; // any value > tMax will do
                    tMax = -1;
                }

                /* if p is inside the slab we do **nothing**:
                   the slab gives no additional restriction */
                return;
            }

            double t1 = (minB - p) / d;
            double t2 = (maxB - p) / d;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
        }

        Slab(fx, dx, bounds.XMin, bounds.XMax);
        Slab(fy, dy, bounds.YMin, bounds.YMax);
        Slab(fz, dz, bounds.ZMin, bounds.ZMax);

        if (tMin > tMax) return; // line misses the box

        ls.SetPoint1(fx + dx * tMin, fy + dy * tMin, fz + dz * tMin);
        ls.SetPoint2(fx + dx * tMax, fy + dy * tMax, fz + dz * tMax);
        ls.Modified();
    }
}