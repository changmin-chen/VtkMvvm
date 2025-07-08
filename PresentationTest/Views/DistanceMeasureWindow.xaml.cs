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

    private readonly vtkDistanceWidget _distanceWidget = vtkDistanceWidget.New();
    private readonly vtkImageActorPointPlacer _placer = vtkImageActorPointPlacer.New();
    private vtkDistanceRepresentation2D _rep;

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

        // Create the distance widget and its 3D representation -------------
        _distanceWidget.SetInteractor(iren);
        _distanceWidget.SetCurrentRenderer(AxialControl.OverlayRenderer);
        _distanceWidget.CreateDefaultRepresentation(); 
        _rep = vtkDistanceRepresentation2D.SafeDownCast(_distanceWidget.GetRepresentation());

        // keep both handles fixed on the slice plane
        vtkImageActor actor = _vm.AxialVm.Actor;
        _placer.SetImageActor(actor);
        _rep.GetPoint1Representation().SetPointPlacer(_placer);
        _rep.GetPoint2Representation().SetPointPlacer(_placer);

        // Configure the visuals
        vtkAxisActor2D axis = _rep.GetAxis();
        axis.GetProperty().SetColor(1, 1, 0);
        axis.GetProperty().SetLineWidth(1);
        axis.SetTitlePosition(1); 
        _rep.SetLabelFormat("%4.2f mm");
        

        // -------------------------------------------------------------------------
        _distanceWidget.EnabledOn();
        iren.Initialize();
        iren.GetRenderWindow().Render();
    }

    private void MeasureDistance(object sender, RoutedEventArgs e)
    {
        double[] p1 = _rep.GetPoint1WorldPosition();
        double[] p2 = _rep.GetPoint2WorldPosition();

        double width = p2[0] - p1[0];
        double height = p2[1] - p1[1];
        double depth = p2[2] - p1[2];

        double distMm = Math.Sqrt(width * width + height * height + depth * depth);
        Debug.WriteLine($"Widget says: {_rep.GetDistance():F2} mm,  manual check: {distMm:F2} mm");
    }

    private void NewInstance(object sender, RoutedEventArgs e)
    {
        // 
    }
}