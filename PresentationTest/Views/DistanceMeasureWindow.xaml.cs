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

        vtkRenderWindowInteractor iren = AxialControl.Interactor;
        vtkInteractorStyleImage style = new();
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

        // now grab that representation so we can tweak it -------------------------
        var rep = (vtkDistanceRepresentation2D)_distanceWidget.GetRepresentation();
        
        // keep both handles fixed on the slice plane
        vtkImageActor actor = _vm.AxialVms[0].Actor;
        _placer.SetImageActor(actor); 
        ((vtkHandleRepresentation)rep.GetPoint1Representation()).SetPointPlacer(_placer);
        ((vtkHandleRepresentation)rep.GetPoint2Representation()).SetPointPlacer(_placer);

        // ---- cosmetics ----------------------------------------------------------
        rep.GetAxisProperty().SetColor(1, 1, 0);
        rep.GetAxis().GetTitleTextProperty().SetColor(1, 0, 0);
        rep.SetLabelFormat("%4.2f mm");
        
        rep.VisibilityOn();
        rep.RulerModeOn();
        _distanceWidget.SetPriority(1);

        // -------------------------------------------------------------------------
        _distanceWidget.EnabledOn();
        iren.Initialize();
        iren.GetRenderWindow().Render();
    }
}