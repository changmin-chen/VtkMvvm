using Kitware.VTK;

namespace VtkMvvm.Features.InteractorStyle;

public sealed class WorldPositionsCapturedEventArgs : EventArgs
{
    public IReadOnlyList<double[]> WorldPositions { get; }

    public WorldPositionsCapturedEventArgs(IReadOnlyList<double[]> positions)
        => WorldPositions = positions ?? throw new ArgumentNullException(nameof(positions));
}

public sealed class FreeHandPickInteractorStyle : vtkInteractorStyle
{
    /// <summary>
    /// Exposes the drawing line width; setting this updates the on-screen actor immediately.
    /// </summary>
    public float LineWidth
    {
        get => _actor2D.GetProperty().GetLineWidth();
        set => _actor2D.GetProperty().SetLineWidth(value);
    }

    /// <summary>
    /// Fired when the user finishes drawing; supplies the picked world‐coordinates.
    /// </summary>
    public event EventHandler<WorldPositionsCapturedEventArgs>? WorldPositionsCaptured;

    private readonly vtkRenderWindow _renderWindow;
    private readonly vtkRenderer _pickRenderer;
    private readonly vtkRenderer _overlayRenderer = vtkRenderer.New();
    private readonly vtkCellPicker _picker = vtkCellPicker.New();

    // VTK pipeline pieces
    private readonly vtkPoints _points = vtkPoints.New();
    private readonly vtkCellArray _cells = vtkCellArray.New();
    private readonly vtkPolyLine _polyLine = vtkPolyLine.New();
    private readonly vtkPolyData _polyData = vtkPolyData.New();
    private readonly vtkPolyDataMapper2D _mapper2D = vtkPolyDataMapper2D.New();
    private readonly vtkActor2D _actor2D = vtkActor2D.New();

    private readonly List<double[]> _worldPositions = new();

    private bool _isDrawing;

    public FreeHandPickInteractorStyle(vtkRenderWindow renderWindow, vtkRenderer pickRenderer, vtkProp pickerProp)
    {
        _renderWindow = renderWindow ?? throw new ArgumentNullException(nameof(renderWindow));
        _pickRenderer = pickRenderer ?? throw new ArgumentNullException(nameof(pickRenderer));
        if (pickRenderer.HasViewProp(pickerProp) == 0)
        {
            throw new ArgumentException("The picker prop must be added to the pick renderer.", nameof(pickerProp));
        }

        // Add the overlay renderer to render annotation polyline
        _overlayRenderer.SetLayer(1); // drawn *after* main renderer
        _overlayRenderer.InteractiveOff(); // optional
        _overlayRenderer.SetActiveCamera(pickRenderer.GetActiveCamera()); // keep cameras in sync
        _renderWindow.SetNumberOfLayers(2);
        _renderWindow.AddRenderer(_overlayRenderer);
        InitializePolylinePipeline();

        // Hook into the mouse events. Overrides base event only works in C++.
        var iren = _renderWindow.GetInteractor();
        iren.LeftButtonPressEvt += OnLeftButtonDown;
        iren.MouseMoveEvt += OnMouseMove;
        iren.LeftButtonReleaseEvt += OnLeftButtonUp;

        _picker.SetTolerance(1e-4); // good for 0.14 mm voxels
        _picker.PickFromListOn(); // pick from list of actors
        _picker.InitializePickList();
        _picker.AddPickList(pickerProp); // pick from this actor only
    }

    private void InitializePolylinePipeline()
    {
        _polyLine.GetPointIds().SetNumberOfIds(0);
        _cells.InsertNextCell(_polyLine);
        _polyData.SetPoints(_points);
        _polyData.SetLines(_cells);
        _mapper2D.SetInput(_polyData);
        _actor2D.SetMapper(_mapper2D);

        _actor2D.PickableOff();
        _actor2D.GetProperty().SetColor(1.0, 0.0, 0.0);
        _actor2D.GetProperty().SetOpacity(0.2);
        _actor2D.GetProperty().SetLineWidth(5); // default line width

        _overlayRenderer.AddActor(_actor2D); // instead of main renderer
    }


    private void OnLeftButtonDown(vtkObject sender, vtkObjectEventArgs e)
    {
        _isDrawing = true;
        _points.Reset();
        _polyLine.GetPointIds().Reset();
        _cells.Reset();
        _worldPositions.Clear();
        _actor2D.VisibilityOn();
    }

    private void OnMouseMove(vtkObject sender, vtkObjectEventArgs e)
    {
        if (!_isDrawing)
        {
            return;
        }

        var pos = GetInteractor().GetEventPosition();
        long id = _points.InsertNextPoint(pos[0], pos[1], 0);
        _polyLine.GetPointIds().InsertNextId(id);

        // Re-wiring the cell array each time keeps it in sync
        _cells.Reset();
        _cells.InsertNextCell(_polyLine);

        // Capture true 3D position if the picker finds geometry under the cursor
        if (_picker.Pick(pos[0], pos[1], 0, _pickRenderer) != 0)
        {
            _worldPositions.Add(_picker.GetPickPosition());
        }

        GetInteractor().GetRenderWindow().Render();
    }

    private void OnLeftButtonUp(vtkObject sender, vtkObjectEventArgs e)
    {
        _isDrawing = false;
        _actor2D.VisibilityOff();
        WorldPositionsCaptured?.Invoke(this, new WorldPositionsCapturedEventArgs(_worldPositions));
    }
}