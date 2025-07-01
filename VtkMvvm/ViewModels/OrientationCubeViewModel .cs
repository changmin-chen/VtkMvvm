using Kitware.VTK;

namespace VtkMvvm.ViewModels;

/// <summary>
/// A small “R L A P S I” orientation cube that can be dropped into an
/// OrientationMarkerWidget **or** added to a secondary renderer.
/// Assume LPS radiological convention.
/// </summary>
[Obsolete("The oblique ViewModel implemented the cube already", true)]
public sealed class OrientationCubeViewModel : VtkElementViewModel
{
    private readonly vtkAnnotatedCubeActor _cube = vtkAnnotatedCubeActor.New();

    private OrientationCubeViewModel()
    {
        // ── default labels & cosmetics ────────────────────────
        _cube.SetXPlusFaceText("L"); // +X Left
        _cube.SetXMinusFaceText("R"); // -X Right
        _cube.SetYPlusFaceText("P"); // +Y Posterior
        _cube.SetYMinusFaceText("A"); // -Y Anterior
        _cube.SetZPlusFaceText("S"); // +Z Superior
        _cube.SetZMinusFaceText("I"); // -Z Inferior

        _cube.GetTextEdgesProperty().SetColor(1, 1, 1); // white outline
        _cube.GetCubeProperty().SetColor(0.25, 0.25, 0.25);
        _cube.SetTextEdgesVisibility(1);

        Actor = _cube; // allow binding in the View layer
    }

    /// <summary>
    /// Factory method – keeps ctor private and obvious.
    /// </summary>
    public static OrientationCubeViewModel Create() => new();

    // Exposed to View
    public override vtkProp Actor { get; }

    #region Bindable properties

    private double _scale = 1.0;

    public double Scale
    {
        get => _scale;
        set
        {
            if (!SetField(ref _scale, value)) return;
            _cube.SetScale(value);
            _cube.Modified();
            OnModified();
        }
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cube.Dispose();
        }
        base.Dispose(disposing);
    }
}