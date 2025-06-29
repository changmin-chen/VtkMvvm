using System.Numerics;
using Kitware.VTK;

namespace VtkMvvm.Controls;

/// <summary>
/// Pure-view helper that adds R/L/A/P/S/I labels to an overlay renderer
/// and keeps them in sync with the camera roll / flip.
/// </summary>
public sealed class OrientationLabelBehaviour : IDisposable
{
    private readonly vtkRenderer _overlay;
    private readonly vtkTextActor _rowPos, _rowNeg, _colPos, _colNeg;
    private readonly Vector3 _rowDir, _colDir; // slice axes (+u, +v) in world
    private readonly vtkCamera _cam;

    public OrientationLabelBehaviour(
        vtkRenderer overlay,
        vtkCamera cam,
        Vector3 rowDir, // +u axis   (e.g. +X on axial)
        Vector3 colDir) // +v axis   (e.g. +Y on axial)
    {
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        _cam = cam ?? throw new ArgumentNullException(nameof(cam));

        _rowDir = Vector3.Normalize(rowDir);
        _colDir = Vector3.Normalize(colDir);

        (_rowPos, _rowNeg, _colPos, _colNeg) = CreateActors();
        UpdateLabels();
        UpdatePlacement(null, null); // initial

        _cam.ModifiedEvt += UpdatePlacement; // react to rolls / flips
    }

    public void Dispose()
    {
        _cam.ModifiedEvt -= UpdatePlacement;
        foreach (var a in new[] { _rowPos, _rowNeg, _colPos, _colNeg })
            a.Dispose();
    }

    // ─────────────────────────────────────────────────────────────

    #region helpers

    private (vtkTextActor, vtkTextActor, vtkTextActor, vtkTextActor) CreateActors()
    {
        vtkTextActor Make()
        {
            var t = vtkTextActor.New();
            t.GetTextProperty().SetFontFamilyToCourier();
            t.GetTextProperty().SetFontSize(18);
            t.GetTextProperty().SetColor(1, 1, 1); // white text on viewer
            _overlay.AddActor2D(t);
            return t;
        }

        return (Make(), Make(), Make(), Make());
    }

    private void UpdateLabels()
    {
        _rowPos.SetInput(TagFor(_rowDir)); // e.g. "R"
        _rowNeg.SetInput(TagFor(-_rowDir)); // e.g. "L"
        _colPos.SetInput(TagFor(_colDir)); // e.g. "A"
        _colNeg.SetInput(TagFor(-_colDir)); // e.g. "P"
    }

    /// <summary>
    /// Re-home every label whenever the camera rolls / flips.
    /// Works for any oblique view because we project axes into
    /// screen-space and decide *per axis* whether it is “more
    /// horizontal” or “more vertical”.
    /// </summary>
    private void UpdatePlacement(vtkObject? sender, vtkObjectEventArgs? e)
    {
        // --- screen basis ------------------------------------------------
        Vector3 vpn = ToV3(_cam.GetViewPlaneNormal()); // toward viewer
        Vector3 vup = Vector3.Normalize(ToV3(_cam.GetViewUp()));
        Vector3 right = Vector3.Normalize(Vector3.Cross(vpn, vup));

        // --- decide which axis is horizontal vs vertical -----------------
        bool rowIsHoriz = Math.Abs(Vector3.Dot(_rowDir, right)) >=
                          Math.Abs(Vector3.Dot(_rowDir, vup));

        // place row-axis labels
        PlaceAxis(_rowDir, _rowPos, _rowNeg, rowIsHoriz, right, vup);

        // col-axis is orthogonal → if row is horiz, col is vert, and vice-versa
        bool colIsHoriz = !rowIsHoriz;
        PlaceAxis(_colDir, _colPos, _colNeg, colIsHoriz, right, vup);

        _overlay.Modified(); // redraw overlay only
    }

    /// Places the “positive” / “negative” label pair of ONE axis
    private static void PlaceAxis(
        Vector3 axisDir,
        vtkTextActor posLabel,
        vtkTextActor negLabel,
        bool axisIsHoriz,
        Vector3 right,
        Vector3 up)
    {
        double mid = 0.50;
        if (axisIsHoriz)
        {
            bool posGoesRight = Vector3.Dot(axisDir, right) >= 0;
            Place(posGoesRight ? posLabel : negLabel, 0.97, mid);
            Place(posGoesRight ? negLabel : posLabel, 0.03, mid);
        }
        else
        {
            bool posGoesUp = Vector3.Dot(axisDir, up) >= 0;
            Place(posGoesUp ? posLabel : negLabel, mid, 0.97);
            Place(posGoesUp ? negLabel : posLabel, mid, 0.03);
        }
    }

    private static void Place(vtkTextActor a, double u, double v)
    {
        var c = a.GetPositionCoordinate();
        c.SetCoordinateSystemToNormalizedViewport();
        c.SetValue(u, v);
    }

    private static Vector3 ToV3(double[] d) => new((float)d[0], (float)d[1], (float)d[2]);

    // RAS mapping  (+X=R, –X=L, +Y=A, –Y=P, +Z=S, –Z=I)
    private static string TagFor(Vector3 v)
    {
        v = Vector3.Normalize(v);
        int axis = 0;
        float max = Math.Abs(v.X);
        if (Math.Abs(v.Y) > max)
        {
            axis = 1;
            max = Math.Abs(v.Y);
        }
        if (Math.Abs(v.Z) > max) axis = 2;

        return axis switch
        {
            0 => v.X > 0 ? "R" : "L",
            1 => v.Y > 0 ? "A" : "P",
            _ => v.Z > 0 ? "S" : "I"
        };
    }

    #endregion
}