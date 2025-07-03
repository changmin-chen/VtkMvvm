using System.Numerics;
using Kitware.VTK;
using VtkMvvm.Models;
using VtkMvvm.ViewModels.Base;

namespace VtkMvvm.ViewModels;

/// <summary>
/// A 3-D brush “stamp” (cylinder) that can be oriented arbitrarily by
/// specifying <see cref="Normal"/>.  By convention the cylinder is authored
/// along +Y, so a normal of (0,1,0) means “no rotation”.
/// </summary>
public sealed class BrushViewModel : VtkElementViewModel
{
    // ── VTK plumbing ────────────────────────────────────────────────
    private readonly vtkCylinderSource _src = vtkCylinderSource.New();
    private readonly vtkTransform _orient = vtkTransform.New();
    private readonly vtkTransformPolyDataFilter _orientFilter = vtkTransformPolyDataFilter.New();
    private readonly vtkTransform _position = vtkTransform.New();
    private readonly vtkTransformPolyDataFilter _positionFilter = vtkTransformPolyDataFilter.New();
    private readonly vtkPolyDataNormals _smoother = vtkPolyDataNormals.New();
    private readonly vtkPolyDataMapper _mapper = vtkPolyDataMapper.New();
    private readonly vtkActor _actor = vtkActor.New();

    // ── bindable state ──────────────────────────────────────────────
    private double _diameter = 2.0;
    private double _height = 2.0;
    private Vector3 _normal = Vector3.UnitY; // default = axial slice (+Y)
    private Double3 _center = Double3.Zero;

    public BrushViewModel()
    {
        // geometry
        SetDiameter(_diameter);
        SetHeight(_height);
        _src.SetResolution(32);

        // orientation chain
        _orientFilter.SetTransform(_orient);
        _orientFilter.SetInputConnection(_src.GetOutputPort());

        _positionFilter.SetTransform(_position);
        _positionFilter.SetInputConnection(_orientFilter.GetOutputPort());

        _smoother.SetInputConnection(_positionFilter.GetOutputPort());
        _smoother.AutoOrientNormalsOn();

        // actor
        _mapper.SetInputConnection(_smoother.GetOutputPort());
        _actor.SetMapper(_mapper);
        _actor.GetProperty().SetColor(1.0, 0.5, 0.3);
        _actor.GetProperty().SetOpacity(0.8);
        _actor.GetProperty().SetRepresentationToWireframe();
        _actor.GetProperty().SetLineWidth(2);

        // initial orientation
        SetNormal(_normal);
    }

    public override vtkActor Actor => _actor;

    /// <summary>
    /// For anyone who needs the raw, centred brush mesh (e.g. for Boolean
    /// ops in a segmentation pipeline).
    /// </summary>
    public vtkAlgorithmOutput GetBrushGeometryPort() =>
        _orientFilter.GetOutputPort();

    // ── Bindable properties ─────────────────────────────────────────

    public double Diameter
    {
        get => _diameter;
        set
        {
            if (!SetField(ref _diameter, value)) return;
            SetDiameter(value);
            OnModified();
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            if (!SetField(ref _height, value)) return;
            SetHeight(value);
            OnModified();
        }
    }

    /// <summary>
    /// **Unit-length** normal of the brush’s stamping plane.  Setting this
    /// rotates the cylinder so its +Y axis aligns with the given vector.
    /// </summary>
    public Vector3 Normal
    {
        get => _normal;
        set
        {
            Vector3 n = Vector3.Normalize(value);
            if (!SetField(ref _normal, n)) return;
            SetNormal(n);
            OnModified();
        }
    }

    /// <summary>
    /// Centre of the brush in world coordinates.
    /// </summary>
    public Double3 Center
    {
        get => _center;
        set
        {
            if (!SetField(ref _center, value)) return;
            _position.Identity();
            _position.Translate(value.X, value.Y, value.Z);
            _positionFilter.Modified();
            OnModified();
        }
    }

    // ── Obsolete shim for legacy callers ────────────────────────────
    [Obsolete("Use Normal (Vector3) instead", true)]
    public SliceOrientation Orientation
    {
        get => Normal switch
        {
            var v when AlmostEquals(v, Vector3.UnitZ) => SliceOrientation.Axial,
            var v when AlmostEquals(v, Vector3.UnitX) => SliceOrientation.Sagittal,
            _ => SliceOrientation.Coronal
        };
        set
        {
            // Convert enum to vector and forward to Normal.
            Normal = value switch
            {
                SliceOrientation.Axial => Vector3.UnitZ,
                SliceOrientation.Sagittal => Vector3.UnitX,
                _ => Vector3.UnitY // coronal
            };
        }
    }

    // ── helpers ─────────────────────────────────────────────────────

    private void SetDiameter(double d)
    {
        _src.SetRadius(d / 2.0);
        _src.Modified();
    }

    private void SetHeight(double h)
    {
        _src.SetHeight(h);
        _src.Modified();
    }

    /// <summary>Re-orients the cylinder so +Y aligns with <paramref name="n"/>.</summary>
    private void SetNormal(Vector3 n)
    {
        _orient.Identity();

        // Default axis (+Y) → target axis (n).
        const float eps = 1e-5f;
        Vector3 from = Vector3.UnitY;
        float dot = Vector3.Dot(from, n);

        if (1 - dot < eps) // already aligned
            return;

        if (1 + dot < eps) // 180° flip – pick any orthogonal axis
        {
            Vector3 axis = Vector3.UnitX;
            _orient.RotateWXYZ(180, axis.X, axis.Y, axis.Z);
        }
        else // general case
        {
            Vector3 axis = Vector3.Normalize(Vector3.Cross(from, n));
            double angle = Math.Acos(dot) * 180.0 / Math.PI;
            _orient.RotateWXYZ(angle, axis.X, axis.Y, axis.Z);
        }

        _orient.Modified();
    }

    /// <remarks>Naïve fuzzy compare so Orientation shim can work.</remarks>
    private static bool AlmostEquals(Vector3 a, Vector3 b, float eps = 1e-4f) => Vector3.DistanceSquared(a, b) < eps * eps;

    // ── disposal ───────────────────────────────────────────────────
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _src.Dispose();
            _orient.Dispose();
            _orientFilter.Dispose();
            _position.Dispose();
            _positionFilter.Dispose();
            _smoother.Dispose();
            _mapper.Dispose();
            _actor.Dispose();
        }
        base.Dispose(disposing);
    }
}