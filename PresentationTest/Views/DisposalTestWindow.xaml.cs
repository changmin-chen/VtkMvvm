using System.Reactive.Disposables;
using System.Windows;
using Kitware.VTK;
using PresentationTest.ViewModels;
using VtkMvvm.Controls;
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

        foreach (var control in new [] {AxialControl, CoronalControl})
        {
            InitializeInteractorStyle(control);
        }
    }
    
    /// <summary>
    /// Each controls has their own instance of freehand interactor
    /// </summary>
    private void InitializeInteractorStyle(IVtkSceneControl control)
    {
        var style = vtkInteractorStyleImage.New();
        vtkRenderWindowInteractor iren = control.Interactor;

        MouseInteractorBuilder.Create(iren, style)
            .LeftDrag((x, y) => _vm.OnControlGetMouseDisplayPosition(control, x, y), k: KeyMask.Alt)
            .Scroll(forward =>
            {
                int increment = forward ? 1 : -1;
                if (ReferenceEquals(control, AxialControl)) _vm.AxialVms[0].SliceIndex += increment;
                else if (ReferenceEquals(control, CoronalControl)) _vm.CoronalVms[0].SliceIndex += increment;
            })
            .Build()
            .DisposeWith(_disposables);
    }
}