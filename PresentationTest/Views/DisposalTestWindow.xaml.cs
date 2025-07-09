using System.Reactive.Disposables;
using System.Windows;
using Kitware.VTK;
using PresentationTest.ViewModels;
using VtkMvvm.Features.InteractorBehavior;

namespace PresentationTest.Views;

public partial class DisposalTestWindow : Window
{
    private readonly CompositeDisposable _disposables = new();
    private DisposalTestWindowViewModel _vm;
    public DisposalTestWindow()
    {
        InitializeComponent();
        Loaded += OnLoadedOnce;
    }

    private void OnLoadedOnce(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedOnce;
        if (DataContext is DisposalTestWindowViewModel vm)
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
}