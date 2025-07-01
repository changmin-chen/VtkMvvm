using System.Numerics;
using Kitware.VTK;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels;

/// <summary>
/// A bull’s-eye marker: <c>RingCount</c> concentric circles that always
/// lie in the plane whose normal is <see cref="Normal"/> and whose centre
/// is <see cref="FocalPoint"/>.  Intended to give a strong “sniper scope”
/// visual cue for sub-millimetre targeting.
/// </summary>
public sealed class BullseyeViewModel : VtkElementViewModel
{
    // ── geometry + VTK plumbing ─────────────────────────────────────
    private readonly List<vtkRegularPolygonSource> _rings = new();
    private readonly vtkAppendPolyData _append = vtkAppendPolyData.New();
    private readonly vtkPolyDataMapper _mapper = vtkPolyDataMapper.New();
    private readonly vtkActor _actor = vtkActor.New();

    // ── user-tweakable state ────────────────────────────────────────
    private Double3 _focalPoint;
    private Vector3 _normal = Vector3.UnitZ; // default: XY plane
    private int _ringCount = 3;
    private double _ringSpacing = 5.0; // mm
    private float _lineWidth = 1.5f;
    private (double R, double G, double B) _color = (1, 1, 0); // yellow

    // ── ctor/factory ────────────────────────────────────────────────
    private BullseyeViewModel() // internal; use Create()
    {
        _mapper.SetInputConnection(_append.GetOutputPort());
        _actor.SetMapper(_mapper);
        _actor.GetProperty().SetOpacity(1); // outline only
        _actor.GetProperty().SetLineWidth(_lineWidth);
        _actor.GetProperty().SetColor(_color.R, _color.G, _color.B);

        RebuildRings(); // initial geometry
    }

    /// <summary>
    /// Factory helper mirroring the pattern in <see cref="CrosshairViewModel"/>.
    /// </summary>
    public static BullseyeViewModel Create(
        Double3 centre,
        Vector3 normal,
        int ringCount = 2,
        double ringSpacing = 2.0 /* mm */)
    {
        return new BullseyeViewModel
        {
            _focalPoint = centre,
            _normal = Vector3.Normalize(normal),
            _ringCount = Math.Max(1, ringCount),
            _ringSpacing = Math.Max(0.1, ringSpacing)
        };
    }

    // ── VtkElementViewModel contract ───────────────────────────────
    public override vtkProp Actor => _actor;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _mapper.Dispose();
            _append.Dispose();
            _actor.Dispose();
            foreach (var r in _rings) r.Dispose();
        }
        base.Dispose(disposing);
    }

    // ── Bindable properties ────────────────────────────────────────
    public Double3 FocalPoint
    {
        get => _focalPoint;
        set
        {
            if (!SetField(ref _focalPoint, value)) return;
            RebuildRings();
            OnModified();
        }
    }

    /// <summary>Unit-length normal of the bull’s-eye plane.</summary>
    public Vector3 Normal
    {
        get => _normal;
        set
        {
            if (!SetField(ref _normal, Vector3.Normalize(value))) return;
            RebuildRings();
            OnModified();
        }
    }

    public int RingCount
    {
        get => _ringCount;
        set
        {
            if (!SetField(ref _ringCount, Math.Max(1, value))) return;
            RebuildRings();
            OnModified();
        }
    }

    /// <summary>Distance between adjacent rings in millimetres.</summary>
    public double RingSpacing
    {
        get => _ringSpacing;
        set
        {
            if (!SetField(ref _ringSpacing, Math.Max(0.1, value))) return;
            RebuildRings();
            OnModified();
        }
    }

    public float LineWidth
    {
        get => _lineWidth;
        set
        {
            if (!SetField(ref _lineWidth, value)) return;
            _actor.GetProperty().SetLineWidth(value);
            _actor.Modified();
            OnModified();
        }
    }

    public (double R, double G, double B) Color
    {
        get => _color;
        set
        {
            if (!SetField(ref _color, value)) return;
            _actor.GetProperty().SetColor(value.R, value.G, value.B);
            _actor.Modified();
            OnModified();
        }
    }

    // ── private helpers ────────────────────────────────────────────
    private void RebuildRings()
    {
        // purge previous sources (VTK objects must be disposed!)
        foreach (var r in _rings) r.Dispose();
        _rings.Clear();
        _append.RemoveAllInputs();

        for (int i = 1; i <= _ringCount; ++i)
        {
            var ring = vtkRegularPolygonSource.New();
            ring.SetNumberOfSides(128); // smooth enough
            ring.GeneratePolygonOff(); // outline only
            ring.SetRadius(i * _ringSpacing);
            ring.SetNormal(_normal.X, _normal.Y, _normal.Z);
            ring.SetCenter(_focalPoint.X, _focalPoint.Y, _focalPoint.Z);
            ring.Update(); // needed before Append

            _append.AddInputConnection(ring.GetOutputPort());
            _rings.Add(ring);
        }

        _append.Update();
        _actor.Modified();
    }
}