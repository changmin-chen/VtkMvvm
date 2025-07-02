using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using Kitware.VTK;
using PresentationTest.ViewModels;
using ReactiveUI;
using VtkMvvm.Controls;
using VtkMvvm.Features.InteractorBehavior;
using VtkMvvm.Models;

namespace PresentationTest.Views;

public partial class VtkMvvmTestWindow : Window
{
    private readonly CompositeDisposable _disposables = new();
    private VtkMvvmTestWindowViewModel _vm;

    public VtkMvvmTestWindow()
    {
        InitializeComponent();
        Loaded += OnLoadedOnce;
    }

    private void OnLoadedOnce(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedOnce;

        if (DataContext is VtkMvvmTestWindowViewModel vm)
        {
            _vm = vm;
        }

        foreach (var ctrl in new[] { AxialControl, CoronalControl, SagittalControl })
        {
            InitializeFreehandInteractor(ctrl);
        }
    }

    /// <summary>
    /// Each controls has their own instance of freehand interactor
    /// </summary>
    private void InitializeFreehandInteractor(VtkImageSceneControl control)
    {
        vtkInteractorStyleImage style = new(); // 被attach的event會直接覆蓋
        vtkRenderWindowInteractor? iren = control.GetInteractor();

        MouseInteractorBuilder.Create(iren, style)
            .LeftMove((x, y) => _vm.OnControlGetBrushPosition(control, x, y))
            .LeftDrag((x, y) => _vm.OnControlGetMouseDisplayPosition(control, x, y), keys: KeyMask.Alt)
            .LeftDrag((x, y) => _vm.OnControlGetMousePaintPosition(control, x, y), keys: KeyMask.None)
            .LeftDragRx(obs => obs
                .Sample(TimeSpan.FromMilliseconds(33))
                .ObserveOn(RxApp.MainThreadScheduler)  // necessary
                .Subscribe(_ => RenderControls()))
            .Scroll(forward =>
            {
                int increment = forward ? 1 : -1;
                switch (control.Orientation)
                {
                    case SliceOrientation.Axial:
                        _vm.AxialSliceIndex += increment;
                        break;
                    case SliceOrientation.Coronal:
                        _vm.CoronalSliceIndex += increment;
                        break;
                    case SliceOrientation.Sagittal:
                        _vm.SagittalSliceIndex += increment;
                        break;
                }
            })
            .Build()
            .DisposeWith(_disposables);
    }


    private void RenderControls()
    {
        AxialControl.Render();
        CoronalControl.Render();
        SagittalControl.Render();
    }
}