using System.Reactive.Linq;
using System.Reactive.Subjects;
using Kitware.VTK;

namespace PresentationTest;

public sealed class WorldPositionsCapturedEventArgs(IReadOnlyList<double[]> positions) : EventArgs
{
    public IReadOnlyList<double[]> WorldPositions { get; } = positions ?? throw new ArgumentNullException(nameof(positions));
}

public sealed class FreeHandPickInteractorStyle : vtkInteractorStyle
{
    private readonly vtkActor2D _actor2D = vtkActor2D.New();
    private readonly vtkCellArray _cells = vtkCellArray.New();
    private readonly vtkPolyDataMapper2D _mapper2D = vtkPolyDataMapper2D.New();

    // Rx
    private readonly Subject<double[]> _moveSubject = new();
    private readonly vtkRenderer _overlayRenderer = vtkRenderer.New();
    private readonly vtkCellPicker _picker = vtkCellPicker.New();
    private readonly vtkRenderer _pickRenderer;

    // VTK pipeline pieces
    private readonly vtkPoints _points = vtkPoints.New();
    private readonly vtkPolyData _polyData = vtkPolyData.New();
    private readonly vtkPolyLine _polyLine = vtkPolyLine.New();

    private readonly vtkRenderWindow _renderWindow;

    private readonly List<double[]> _worldPositions = new();
    private bool _disposed;

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

    public IObservable<double[]> Moves
    {
        get => _moveSubject.AsObservable();
    }

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
            double[]? worldPos = _picker.GetPickPosition();
            _moveSubject.OnNext(worldPos);
            _worldPositions.Add(worldPos);
        }

        GetInteractor().GetRenderWindow().Render();
    }

    private void OnLeftButtonUp(vtkObject sender, vtkObjectEventArgs e)
    {
        _isDrawing = false;
        _actor2D.VisibilityOff();
        WorldPositionsCaptured?.Invoke(this, new WorldPositionsCapturedEventArgs(_worldPositions));
    }

    /// <summary>
    /// Override the base-class Dispose to clean up our extra VTK objects and event handlers.
    /// </summary>
    /// <param name="disposing">True when called from Dispose(), false from finalizer.</param>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // 1) Unsubscribe from the interactor events
            vtkRenderWindowInteractor? iren = _renderWindow.GetInteractor();
            iren.LeftButtonPressEvt -= OnLeftButtonDown;
            iren.MouseMoveEvt -= OnMouseMove;
            iren.LeftButtonReleaseEvt -= OnLeftButtonUp;

            // 2) Remove our overlay renderer so VTK stops holding a reference
            _renderWindow.RemoveRenderer(_overlayRenderer);

            // 3) Dispose all the VTK pipeline pieces we New()’d
            _overlayRenderer.Dispose();
            _picker.Dispose();
            _points.Dispose();
            _cells.Dispose();
            _polyLine.Dispose();
            _polyData.Dispose();
            _mapper2D.Dispose();
            _actor2D.Dispose();
        }

        _disposed = true;

        // 4) Let the base class clean up its own resources
        base.Dispose(disposing);
    }
}