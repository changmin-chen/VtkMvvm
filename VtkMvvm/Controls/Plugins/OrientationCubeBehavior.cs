using Kitware.VTK;

namespace VtkMvvm.Controls.Plugins;

/// <summary>
/// Adds a radiological "R L A P S I" orientation cube to the supplied
/// <see cref="vtkRenderWindow"/> and keeps it permanently docked in the
/// bottom-left corner, independent of the main camera.
/// </summary>
public sealed class OrientationCubeBehavior : IDisposable
{
    private readonly vtkAnnotatedCubeActor _cube = vtkAnnotatedCubeActor.New();
    private readonly vtkOrientationMarkerWidget _widget = vtkOrientationMarkerWidget.New();
    private readonly vtkPropAssembly _assembly;

    public OrientationCubeBehavior(vtkRenderWindow renderWindow)
    {
        // ── widget ─────────────────────────────────────────────
        _widget.SetInteractor(renderWindow.GetInteractor());
        _assembly = MakeMedicalOrientationCube();
        _widget.SetOrientationMarker(_assembly);
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
        _assembly?.Dispose();
    }

    private static vtkPropAssembly MakeMedicalOrientationCube()
    {
        // ----- 1.  Lettered cube (faces fully transparent) -----------------
        var ann = vtkAnnotatedCubeActor.New();
        ann.SetXPlusFaceText("L");
        ann.SetXMinusFaceText("R");
        ann.SetYPlusFaceText("P");
        ann.SetYMinusFaceText("A");
        ann.SetZPlusFaceText("S");
        ann.SetZMinusFaceText("I");

        ann.SetFaceTextScale(0.6); // bigger letters
        ann.GetTextEdgesProperty().SetColor(1, 1, 1); // white outline
        ann.SetTextEdgesVisibility(1);
        ann.GetCubeProperty().SetOpacity(0.0); // hide grey faces

        // white letters so they work on any face colour
        foreach (var p in new[]
                 {
                     ann.GetXPlusFaceProperty(), ann.GetXMinusFaceProperty(),
                     ann.GetYPlusFaceProperty(), ann.GetYMinusFaceProperty(),
                     ann.GetZPlusFaceProperty(), ann.GetZMinusFaceProperty()
                 })
            p.SetColor(1, 1, 1);

        // ----- 2.  Coloured cube ------------------------------------------
        var cubeSrc = vtkCubeSource.New();
        cubeSrc.Update();

        // colours have to be unsigned-char triples (0-255)
        var cols = vtkUnsignedCharArray.New();
        cols.SetNumberOfComponents(3);

        // order: -X, +X, -Y, +Y, -Z, +Z  (VTK face order)
        cols.InsertNextTuple3(76, 153, 255); // R  (light-blue)
        cols.InsertNextTuple3(26, 90, 217); // L  (dark-blue)
        cols.InsertNextTuple3(255, 141, 141); // A  (light-red)
        cols.InsertNextTuple3(230, 55, 55); // P  (red)
        cols.InsertNextTuple3(140, 255, 153); // I  (light-green)
        cols.InsertNextTuple3(51, 204, 64); // S  (green)

        var pd = cubeSrc.GetOutput();
        pd.GetCellData().SetScalars(cols);

        var mapper = vtkPolyDataMapper.New();
        mapper.SetInput(pd);
        var cubeActor = vtkActor.New();
        cubeActor.SetMapper(mapper);

        // Clean up intermediate objects
        cols.Dispose();
        cubeSrc.Dispose();

        // ----- 3.  Combine the two props ----------------------------------
        var assembly = vtkPropAssembly.New();
        assembly.AddPart(cubeActor);
        assembly.AddPart(ann);

        // Note: cubeActor, mapper, and ann are now owned by the assembly
        // and will be disposed when the assembly is disposed
        mapper.Dispose();
        cubeActor.Dispose();
        ann.Dispose();

        return assembly;
    }
}