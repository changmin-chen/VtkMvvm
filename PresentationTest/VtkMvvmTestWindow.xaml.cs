using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using Kitware.VTK;
using ReactiveUI;
using VtkMvvm.Controls;
using VtkMvvm.Features.InteractorBehavior;

namespace PresentationTest;

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

        InitializeFreehandInteractor([AxialControl, CoronalControl, SagittalControl]);
    }

    /// <summary>
    /// Each controls has their own instance of freehand interactor
    /// </summary>
    private void InitializeFreehandInteractor(IEnumerable<VtkImageSceneControl> controls)
    {
        foreach (var control in controls)
        {
            vtkInteractorStyleImage style = new(); // 被attach的event會直接覆蓋
            vtkRenderWindowInteractor? iren = control.RenderWindowControl.RenderWindow.GetInteractor();

            MouseInteractorBehavior leftBehavior = new(TriggerMouseButton.Left);

            leftBehavior.AttachTo(style);

            iren.SetInteractorStyle(style);
            iren.Initialize();

            // Left mouse: paint and render
            leftBehavior.Moves
                .Subscribe(pos => { _vm.OnControlGetBrushPosition(control, pos.x, pos.y); });

            IObservable<(int x, int y)> leftMouseDrag = leftBehavior.Moves
                .Where(_ => leftBehavior.IsPressing); // should move + pressing

            leftMouseDrag
                .Subscribe(pos => { _vm.OnControlGetMousePaintPosition(control, pos.x, pos.y); })
                .DisposeWith(_disposables);

            leftMouseDrag
                .Sample(TimeSpan.FromMilliseconds(33), RxApp.MainThreadScheduler)
                .Subscribe(_ => RenderControls())
                .DisposeWith(_disposables); // render every 33ms if paint

            _disposables.Add(leftBehavior);
        }
    }


    private void RenderControls()
    {
        AxialControl.RenderWindowControl.RenderWindow.Render();
        CoronalControl.RenderWindowControl.RenderWindow.Render();
        SagittalControl.RenderWindowControl.RenderWindow.Render();
    }
}