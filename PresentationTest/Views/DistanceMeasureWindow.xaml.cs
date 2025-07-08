using System.Diagnostics;
using System.Reactive.Disposables;
using System.Windows;
using Kitware.VTK;
using PresentationTest.ViewModels;
using VtkMvvm.Features.InteractorBehavior;

namespace PresentationTest.Views;

public partial class DistanceMeasureWindow : Window
{
    private readonly CompositeDisposable _disposables = new();
    private DistanceMeasureWindowViewModel _vm;

    private readonly Stack<vtkDistanceWidget> _measureWidgets = new();
    private readonly vtkImageActorPointPlacer _placer = vtkImageActorPointPlacer.New();

    public DistanceMeasureWindow()
    {
        InitializeComponent();
        Loaded += OnLoadedOnce;
    }

    private void OnLoadedOnce(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedOnce;
        if (DataContext is DistanceMeasureWindowViewModel vm)
        {
            _vm = vm;
        }

        // scroll-to-next-slice 
        vtkRenderWindowInteractor iren = AxialControl.Interactor;
        var style = vtkInteractorStyleImage.New();
        MouseInteractorBuilder.Create(iren, style)
            .Scroll(forward =>
            {
                int increment = forward ? 1 : -1;
                _vm.AxialVms[0].SliceIndex += increment;
            })
            .Build()
            .DisposeWith(_disposables);
    }

    private void MeasureDistance(object sender, RoutedEventArgs e)
    {
        if (_measureWidgets.Count == 0) return;
        var widget = _measureWidgets.Peek();
        var rep = vtkDistanceRepresentation2D.SafeDownCast(widget.GetRepresentation());

        double[] p1 = rep.GetPoint1WorldPosition();
        double[] p2 = rep.GetPoint2WorldPosition();
        double width = p2[0] - p1[0];
        double height = p2[1] - p1[1];
        double depth = p2[2] - p1[2];

        double distMm = Math.Sqrt(width * width + height * height + depth * depth);
        Debug.WriteLine($"Widget says: {rep.GetDistance():F2} mm,  manual check: {distMm:F2} mm");
    }

    private void AddMeasurement(object sender, RoutedEventArgs e)
    {
        // 1) New distance widget
        var widget = vtkDistanceWidget.New();
        var iren = AxialControl.Interactor;

        // 2) Hook up interactor & renderer
        widget.SetInteractor(iren);
        widget.SetCurrentRenderer(AxialControl.OverlayRenderer);

        // 3) Build its default rep
        widget.CreateDefaultRepresentation();
        var rep = vtkDistanceRepresentation2D.SafeDownCast(widget.GetRepresentation());

        // 4) Constrain the two “+” to your image‐slice plane
        vtkImageActor actor = _vm.AxialVm.Actor;
        _placer.SetImageActor(actor);
        rep.GetPoint1Representation().SetPointPlacer(_placer);
        rep.GetPoint2Representation().SetPointPlacer(_placer);
        SetShape(rep);

        // 5) Style the line + text
        var axis = rep.GetAxis();
        axis.GetProperty().SetColor(1, 1, 0); // yellow
        axis.GetProperty().SetLineWidth(1);
        rep.SetLabelFormat("%4.2f mm");
        var textProperty = axis.GetTitleTextProperty();
        textProperty.ItalicOff();
        textProperty.SetFontFamilyToCourier();

        // 6) Style the handles (optional)
        var h1 = vtkPointHandleRepresentation2D.SafeDownCast(rep.GetPoint1Representation());
        var h2 = vtkPointHandleRepresentation2D.SafeDownCast(rep.GetPoint2Representation());
        h1.GetProperty().SetColor(1, 0, 0); // red crosses
        h2.GetProperty().SetColor(1, 0, 0);

        // 7) Enable it (starts in “placement” mode)
        widget.EnabledOn();

        // 9) Save it so you can delete or iterate later
        _measureWidgets.Push(widget);

        // 10) Render
        iren.GetRenderWindow().Render();
    }

    private static void SetShape(vtkDistanceRepresentation2D rep)
    {
        const double halfSize = 9;
        using var cursor = vtkCursor2D.New();
        cursor.SetModelBounds(-halfSize, halfSize, -halfSize, halfSize, 0, 0);
        cursor.SetFocalPoint(0, 0, 0);
        cursor.OutlineOff();
        
        var h1 = vtkPointHandleRepresentation2D.SafeDownCast(rep.GetPoint1Representation());
        var h2 = vtkPointHandleRepresentation2D.SafeDownCast(rep.GetPoint2Representation());
        h1.SetCursorShape(cursor.GetOutput());
        h2.SetCursorShape(cursor.GetOutput());
       
        h1.GetProperty().SetLineWidth(2);
        h2.GetProperty().SetLineWidth(2);
        h1.GetSelectedProperty().SetLineWidth(3);
        h2.GetSelectedProperty().SetLineWidth(3);
    }
}