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

    public OrientationCubeBehavior(vtkRenderWindow renderWindow)
    {
        // ── labels ─────────────────────────────────────────────
        _cube.SetXPlusFaceText("L");
        _cube.SetXMinusFaceText("R");
        _cube.SetYPlusFaceText("P");
        _cube.SetYMinusFaceText("A");
        _cube.SetZPlusFaceText("S");
        _cube.SetZMinusFaceText("I");

        _cube.SetFaceTextScale(0.6);
        _cube.GetCubeProperty().SetColor(0.25, 0.25, 0.25);

        // white letters + outline
        foreach (var prop in new[]
                 {
                     _cube.GetXPlusFaceProperty(), _cube.GetXMinusFaceProperty(),
                     _cube.GetYPlusFaceProperty(), _cube.GetYMinusFaceProperty(),
                     _cube.GetZPlusFaceProperty(), _cube.GetZMinusFaceProperty()
                 })
            prop.SetColor(1, 1, 1);

        _cube.GetTextEdgesProperty().SetColor(1, 1, 1);
        _cube.SetTextEdgesVisibility(1);

        // ── widget ─────────────────────────────────────────────
        _widget.SetInteractor(renderWindow.GetInteractor());
        _widget.SetOrientationMarker(_cube);
        _widget.SetViewport(0.00, 0.00, 0.15, 0.15); // bottom-left corner
        _widget.SetEnabled(1); // enable first!
        _widget.InteractiveOff(); // then lock
    }

    public double Scale
    {
        get => _cube.GetScale()[0];
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