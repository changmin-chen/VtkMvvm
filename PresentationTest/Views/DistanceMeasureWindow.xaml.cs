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

        vtkInteractorStyleImage style = new();
        vtkRenderWindowInteractor iren = AxialControl.Interactor;

        MouseInteractorBuilder.Create(iren, style)
            .Scroll(forward =>
            {

                int increment = forward ? 1 : -1;
                _vm.AxialVms[0].SliceIndex += increment;
            })
            .Build()
            .DisposeWith(_disposables);

        
        
    }
}