using System.Numerics;
using Kitware.VTK;

namespace VtkMvvm.Controls.Plugins;

/// <summary>
/// Draws four orientation labels (R / L / A / P / S / I) on
/// overlay renderer and keeps them aligned with the
/// patient axes for any camera roll / flip. Uses LPS convention.
/// </summary>
public sealed class OrientationLabelBehavior : IDisposable
{
    private readonly vtkRenderer _overlay;
    private readonly vtkCamera _cam;

    private readonly vtkTextActor _lblRight;
    private readonly vtkTextActor _lblLeft;
    private readonly vtkTextActor _lblTop;
    private readonly vtkTextActor _lblBottom;

    // 6 patient-axis unit vectors.
    // Assume image come from DICOM (LPS): +X=L, +Y=P, +Z=S
    private static readonly (string Tag, Vector3 Dir)[] PatientAxes =
    [
        ("L", Vector3.UnitX),
        ("R", -Vector3.UnitX),
        ("P", Vector3.UnitY),
        ("A", -Vector3.UnitY),
        ("S", Vector3.UnitZ),
        ("I", -Vector3.UnitZ)
    ];

    public OrientationLabelBehavior(vtkRenderer overlay, vtkCamera cam)
    {
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        _cam = cam ?? throw new ArgumentNullException(nameof(cam));

        (_lblRight, _lblLeft, _lblTop, _lblBottom) = MakeActors();
        UpdateLabels(null, null); // initial placement
        _overlay.EndEvt += UpdateLabels;  // follow every roll / flip
    }

    public void Dispose()
    {
        _overlay.EndEvt -= UpdateLabels;
        foreach (var a in new[] { _lblRight, _lblLeft, _lblTop, _lblBottom })
            a.Dispose();
    }

    // ─────────────────────────────────────────────────────────────

    #region helpers

    private (vtkTextActor, vtkTextActor, vtkTextActor, vtkTextActor) MakeActors()
    {
        vtkTextActor Make()
        {
            var t = vtkTextActor.New();
            t.GetTextProperty().SetFontFamilyToCourier();
            t.GetTextProperty().SetFontSize(20);
            t.GetTextProperty().SetColor(1, 1, 1);  // white 
            _overlay.AddActor2D(t);
            return t;
        }

        return (Make(), Make(), Make(), Make());
    }

    /// Re-positions every label after ANY camera change.
    private void UpdateLabels(vtkObject? s, vtkObjectEventArgs? e)
    {
        // 1.  Display coords of the slice centre (focal point)
        double[] f = _cam.GetFocalPoint();
        var displayCentre = WorldToDisplay(f);

        // 2.  Map each patient axis to screen Δ(x,y)
        var screenVectors = PatientAxes.Select(ax =>
        {
            var p = new[] { f[0] + ax.Dir.X, f[1] + ax.Dir.Y, f[2] + ax.Dir.Z };
            var d = WorldToDisplay(p);

            return (ax.Tag,
                dx: d.X - displayCentre.X,
                dy: d.Y - displayCentre.Y);
        }).ToArray();

        // 3.  Pick ONE tag per edge (max |Δ| wins)
        string tagRight = screenVectors.MaxBy(v => v.dx).Tag;
        string tagLeft = screenVectors.MinBy(v => v.dx).Tag;
        string tagTop = screenVectors.MaxBy(v => v.dy).Tag;
        string tagBottom = screenVectors.MinBy(v => v.dy).Tag;

        _lblRight.SetInput(tagRight);
        _lblLeft.SetInput(tagLeft);
        _lblTop.SetInput(tagTop);
        _lblBottom.SetInput(tagBottom);

        // 4.  Update positions (normalised viewport)
        Place(_lblRight, 0.97, 0.50);
        Place(_lblLeft, 0.03, 0.50);
        Place(_lblTop, 0.50, 0.97);
        Place(_lblBottom, 0.50, 0.03);

        _overlay.Modified(); // redraw overlay only
    }

    private Vector3 WorldToDisplay(IReadOnlyList<double> world)
    {
        _overlay.SetWorldPoint(world[0], world[1], world[2], 1.0);
        _overlay.WorldToDisplay();
        double[] d = _overlay.GetDisplayPoint();
        return new Vector3((float)d[0], (float)d[1], (float)d[2]);
    }

    private static void Place(vtkTextActor a, double u, double v)
    {
        var c = a.GetPositionCoordinate();
        c.SetCoordinateSystemToNormalizedViewport();
        c.SetValue(u, v);
    }

    #endregion
}