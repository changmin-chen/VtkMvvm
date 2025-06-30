using System.Numerics;
using Kitware.VTK;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels;

/// <summary>
/// Flat “R / L / A / P / H/ F” text hints glued to the viewport edges.
/// Call <see cref="UpdateFromDirectionCosines"/> every time the slice
/// orientation changes (e.g., user rotates an oblique view).
/// </summary>
/// <remarks>
/// This class assumes the VTK RAS orientation scene setup.
/// If you set the vtkCamera to have negative ViewUp value or looking from opposite direction,
/// the label currently won't sync with camera and will be placed at wrong opposite side!!! 
/// </remarks>
[Obsolete("Use OrientationLabelBehaviour directly in View instead", true)]
public sealed class OrientationLabelsViewModel : VtkElementViewModel
{
    private readonly vtkPropAssembly _assembly = vtkPropAssembly.New();
    private readonly vtkTextActor _lblRight, _lblLeft, _lblTop, _lblBottom;

    private OrientationLabelsViewModel()
    {
        // ── helper local ───────────────────────────────────────
        vtkTextActor Make(string txt, double u, double v)
        {
            var t = vtkTextActor.New();
            t.SetInput(txt);
            t.GetTextProperty().SetFontFamilyToCourier();
            t.GetTextProperty().SetFontSize(16);
            t.GetTextProperty().SetColor(1, 1, 1); // white
            var c = t.GetPositionCoordinate();
            c.SetCoordinateSystemToNormalizedViewport();
            c.SetValue(u, v);
            _assembly.AddPart(t);
            return t;
        }

        // create the four corner-ish labels (normalised viewport coords)
        _lblRight = Make("R", 0.97, 0.5);
        _lblLeft = Make("L", 0.03, 0.5);
        _lblTop = Make("A", 0.5, 0.97);
        _lblBottom = Make("P", 0.5, 0.03);

        Actor = _assembly;
    }

    public static OrientationLabelsViewModel Create() => new();

    /// The single bindable prop (a bundle of 2-D actors)
    public override vtkProp Actor { get; }

    #region Bindable settings (font size, colour…)

    private int _fontSize = 16;

    public int FontSize
    {
        get => _fontSize;
        set
        {
            if (!SetField(ref _fontSize, value)) return;
            foreach (vtkTextActor a in new[] { _lblRight, _lblLeft, _lblTop, _lblBottom })
                a.GetTextProperty().SetFontSize(value);
            _assembly.Modified();
            OnModified();
        }
    }

    #endregion

    // ── public API ──────────────────────────────────────────────
    /// <summary>
    /// Update the label letters so that +row/+col axes of the
    /// current image slice map to patient-orientation tags.
    /// </summary>
    public void UpdateFromDirectionCosines(in Vector3 rowDir, in Vector3 colDir)
    {
        // make sure they're properly unit - helps avoid NANs
        Vector3 row = Vector3.Normalize(rowDir);
        Vector3 col = Vector3.Normalize(colDir);

        _lblRight.SetInput(GetTag(row));
        _lblLeft.SetInput(GetTag(-row));
        _lblTop.SetInput(GetTag(col));
        _lblBottom.SetInput(GetTag(-col));

        _assembly.Modified();
        OnModified();
    }

    public void UpdateFromOrientation(SliceOrientation orientation)
    {
        // Row (u-axis) and Column (v-axis) directions for each canonical view
        (Vector3 row, Vector3 col) dirs = orientation switch
        {
            SliceOrientation.Axial => (Vector3.UnitX, Vector3.UnitY), // R/L  –  A/P
            SliceOrientation.Coronal => (Vector3.UnitX, Vector3.UnitZ), // R/L  –  S/I
            SliceOrientation.Sagittal => (Vector3.UnitY, Vector3.UnitZ), // A/P  –  H/F
            _ => throw new ArgumentOutOfRangeException(nameof(orientation)),
        };

        UpdateFromDirectionCosines(dirs.row, dirs.col);
    }

    // ── helpers ────────────────────────────────────────────────
    private static string GetTag(in Vector3 v)
    {
        // Patient axes:  +X R, –X L, +Y A, –Y P, +Z H, –Z F
        Vector3 n = Vector3.Normalize(v);
        int majorAxis = 0;
        float maxVal = Math.Abs(n.X);

        if (Math.Abs(n.Y) > maxVal)
        {
            maxVal = Math.Abs(n.Y);
            majorAxis = 1;
        }
        if (Math.Abs(n.Z) > maxVal)
        {
            majorAxis = 2;
        }

        // Note that VTK use RAS system
        return majorAxis switch
        {
            0 => n.X > 0 ? "R" : "L", // +X = Right
            1 => n.Y > 0 ? "A" : "P", // +Y = Anterior
            _ => n.Z > 0 ? "H" : "F" 
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (vtkTextActor a in new[] { _lblRight, _lblLeft, _lblTop, _lblBottom })
                a.Dispose();
            _assembly.Dispose();
        }
        base.Dispose(disposing);
    }
}