using System.Diagnostics;
using System.Numerics;
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

    private vtkCamera _camera;
    
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

    private void HandleCamera(vtkObject sender, vtkObjectEventArgs e)
    {
        Vector3 vpn = ToVector3(_camera.GetViewPlaneNormal());  // points *towards* camera
        Vector3 vup = Vector3.Normalize(ToVector3(_camera.GetViewUp()));
        Vector3 right = Vector3.Normalize(Vector3.Cross(vpn, vup));   // screen-right
   
        Debug.WriteLine($"vpn: {vpn}, vup: {vup}, right: {right}");
    }
    
    private static Vector3 ToVector3(double[] da) => new Vector3((float)da[0], (float)da[1], (float)da[2]);
    

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
            ScrollInteractorBehavior scrollBehavior = new() { OverrideBaseStyle = true };
            leftBehavior.AttachTo(style);
            scrollBehavior.AttachTo(style);
            iren.SetInteractorStyle(style);
            iren.Initialize();

            // Left mouse paint and render
            leftBehavior.Moves
                .Subscribe(pos => { _vm.OnControlGetBrushPosition(control, pos.x, pos.y); });

            // should move + pressing
            IObservable<(int x, int y)> leftMouseDrag = leftBehavior.Moves
                .Where(_ => leftBehavior.IsPressing);

            leftMouseDrag
                .Subscribe(pos =>
                {
                    if (iren.GetAltKey() != 0)
                        _vm.OnControlGetMouseDisplayPosition(control, pos.x, pos.y);
                    else
                        _vm.OnControlGetMousePaintPosition(control, pos.x, pos.y);
                })
                .DisposeWith(_disposables);

            leftMouseDrag
                .Sample(TimeSpan.FromMilliseconds(33), RxApp.MainThreadScheduler)
                .Subscribe(_ => RenderControls())
                .DisposeWith(_disposables); // render every 33ms if paint

            // Scroll: change slice index based on view
            scrollBehavior.Scrolls
                .Subscribe(forward =>
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
                .DisposeWith(_disposables);

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