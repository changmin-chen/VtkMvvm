using Kitware.VTK;

namespace VtkMvvm.Controls.Plugins;

/// <summary>
/// Adds a radiological “R L A P S I” orientation cube to the supplied
/// <see cref="vtkRenderWindow"/> and keeps it permanently docked in the
/// bottom-left corner, independent of the main camera.
/// </summary>
public sealed class OrientationCubeBehavior : IDisposable
{
    private readonly vtkAnnotatedCubeActor _cube = vtkAnnotatedCubeActor.New();
    private readonly vtkOrientationMarkerWidget _widget = vtkOrientationMarkerWidget.New();

    /// <param name="renderWindow">
    /// The window that hosts your main & overlay renderers
    /// (e.g. <c>RenderWindowControl.RenderWindow</c>).
    /// </param>
    public OrientationCubeBehavior(vtkRenderWindow renderWindow)
    {
        // ── 1. Cube cosmetics ──────────────────────────────────────────────
        _cube.SetXPlusFaceText("L"); // +X Left
        _cube.SetXMinusFaceText("R"); // -X Right
        _cube.SetYPlusFaceText("P"); // +Y Posterior
        _cube.SetYMinusFaceText("A"); // -Y Anterior
        _cube.SetZPlusFaceText("S"); // +Z Superior
        _cube.SetZMinusFaceText("I"); // -Z Inferior

        _cube.GetTextEdgesProperty().SetColor(1, 1, 1);     // white outline
        _cube.GetCubeProperty().SetColor(0.25, 0.25, 0.25); // dark grey
        _cube.SetTextEdgesVisibility(1);

        // ── 2. Widget setup ────────────────────────────────────────────────
        _widget.SetOrientationMarker(_cube);
        _widget.SetInteractor(renderWindow.GetInteractor());
        _widget.SetViewport(0.00, 0.00, 0.15, 0.15); // bottom-left 15 % of screen
        _widget.InteractiveOff();                    // fixed in place
        _widget.SetEnabled(1);                       // show it
    }

    /// <summary> Uniformly scales the cube. 1 = original size. </summary>
    public double Scale
    {
        get => _cube.GetScale()[0];        // any axis is fine – all three equal
        set
        {
            _cube.SetScale(value);
            _cube.Modified();
        }
    }

    public void Dispose()
    {
        _widget?.Dispose();
        _cube?.Dispose();
    }
}