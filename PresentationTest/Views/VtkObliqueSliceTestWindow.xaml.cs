using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using Kitware.VTK;
using PresentationTest.ViewModels;
using VtkMvvm.Controls;
using VtkMvvm.Features.InteractorBehavior;

namespace PresentationTest.Views;

public partial class VtkObliqueSliceTestWindow : Window
{
    private readonly CompositeDisposable _disposables = new();
    private VtkObliqueSliceTestWindowViewModel _vm;

    public VtkObliqueSliceTestWindow()
    {
        InitializeComponent();
        Loaded += OnLoadedOnce;
    }

    private void OnLoadedOnce(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedOnce;

        if (DataContext is VtkObliqueSliceTestWindowViewModel vm)
        {
            _vm = vm;
        }

        InitializeInteractor(ObliqueControl);
    }

    /// <summary>
    /// Each controls has their own instance of freehand interactor
    /// </summary>
    private void InitializeInteractor(VtkObliqueImageSceneControl control)
    {
        vtkInteractorStyleImage style = new();
        vtkRenderWindowInteractor? iren = control.RenderWindowControl.RenderWindow.GetInteractor();

        var leftBehavior = new MouseInteractorBehavior(TriggerMouseButton.Left);
        leftBehavior.AttachTo(style);
        iren.SetInteractorStyle(style);
        iren.Initialize();

        leftBehavior.Moves.Where(_ => leftBehavior.IsPressing)
            .Subscribe(pos => { _vm.OnControlGetMouseDisplayPosition(control, pos.x, pos.y); })
            .DisposeWith(_disposables);
    }
}