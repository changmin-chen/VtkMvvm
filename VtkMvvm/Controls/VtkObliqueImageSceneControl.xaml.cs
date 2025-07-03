using System.ComponentModel;
using System.Numerics;
using System.Windows;
using System.Windows.Threading;
using Kitware.VTK;
using VtkMvvm.Controls.Plugins;
using VtkMvvm.ViewModels.Base;
using UserControl = System.Windows.Controls.UserControl;

namespace VtkMvvm.Controls;

/// <summary>
/// For binding to the image slice that may not be orthogonal.
/// </summary>
public partial class VtkObliqueImageSceneControl : UserControl, IDisposable, IVtkSceneControl
{
    private const double CamDist = 500; // mm
    private bool _isLoaded; // flag indicates the control is loaded.

    private ImageSliceViewModel? _referenceSlice; // use first oblique slice as reference to place and rotate the camera

    // ---------- Plugins --------------------------------------- 
    private OrientationLabelBehavior? _orientationLabels;  // L,R,P,A,S,I text labels on screen edges
    private OrientationCubeBehavior? _orientationCube; // L,R,P,A,S,I labeled cube fixed at screen bottom-left corner 


    // --- Dependency Properties -------------------------------------------------------- 

    public static readonly DependencyProperty SceneObjectsProperty = DependencyProperty.Register(
        nameof(SceneObjects),
        typeof(IReadOnlyList<ImageSliceViewModel>),
        typeof(VtkObliqueImageSceneControl),
        new PropertyMetadata(null, OnImageObjectsChanged));

    public static readonly DependencyProperty OverlayObjectsProperty = DependencyProperty.Register(
        nameof(OverlayObjects),
        typeof(IReadOnlyList<VtkElementViewModel>),
        typeof(VtkObliqueImageSceneControl),
        new PropertyMetadata(null, OnOverlayObjectsChanged));

    public IReadOnlyList<VtkElementViewModel>? SceneObjects
    {
        get => (IReadOnlyList<VtkElementViewModel>)GetValue(SceneObjectsProperty);
        set => SetValue(SceneObjectsProperty, value);
    }

    public IReadOnlyList<VtkElementViewModel>? OverlayObjects
    {
        get => (IReadOnlyList<VtkElementViewModel>)GetValue(OverlayObjectsProperty);
        set => SetValue(OverlayObjectsProperty, value);
    }

    public vtkRenderer MainRenderer { get; } = vtkRenderer.New();
    public vtkRenderer OverlayRenderer { get; } = vtkRenderer.New();
    public RenderWindowControl RenderWindowControl { get; } = new();
    public vtkCamera GetActiveCamera() => MainRenderer.GetActiveCamera();
    public vtkRenderWindowInteractor GetInteractor() => RenderWindowControl.RenderWindow.GetInteractor();
    public void Render() => RenderWindowControl.RenderWindow.Render();

    public VtkObliqueImageSceneControl()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this)) return;

        RenderWindowControl.Dock = DockStyle.Fill;
        WFHost.Child = RenderWindowControl;
        MainRenderer.GetActiveCamera().ParallelProjectionOn();

        Loaded += OnLoadedOnce;
    }

    private void OnLoadedOnce(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedOnce;

        var renderWindow = RenderWindowControl.RenderWindow;
        if (renderWindow is null) throw new InvalidOperationException("Render window expects to be non-null at this point.");

        renderWindow.AddRenderer(MainRenderer);
        MainRenderer.SetBackground(0.0, 0.0, 0.0);
        MainRenderer.SetLayer(0);
        OverlayRenderer.SetLayer(1);  // render overlays onto the main renderer
        OverlayRenderer.PreserveDepthBufferOff();
        OverlayRenderer.InteractiveOff();
        OverlayRenderer.SetActiveCamera(MainRenderer.GetActiveCamera()); // keep cameras in sync
        renderWindow.SetNumberOfLayers(2);
        renderWindow.AddRenderer(OverlayRenderer);
        
        _isLoaded = true;

        // ── orientation cube ───────────────────────────────
        // If the attached property was set before the control was loaded,
        // this will ensure the cube is created now.
        if (ControlPlugin.GetOrientationCube(this))
        {
            AddOrientationCube();
        }
        if (ControlPlugin.GetOrientationLabels(this))
        {
            AddOrientationLabels();
        }
    }
    
    public void AddOrientationCube()
    {
        if (_isLoaded && _orientationCube == null)
        {
            _orientationCube = new OrientationCubeBehavior(RenderWindowControl.RenderWindow);
            RequestRender(); // Render to show the cube
        }
    }

    public void RemoveOrientationCube()
    {
        if (_orientationCube != null)
        {
            _orientationCube.Dispose();
            _orientationCube = null;
            RequestRender(); // Render to reflect the removal
        }
    }

    public void AddOrientationLabels()
    {
        if (_isLoaded && _orientationLabels == null)
        {
            _orientationLabels = new OrientationLabelBehavior(OverlayRenderer, MainRenderer.GetActiveCamera());
            RequestRender(); 
        }
    }

    public void RemoveOrientationLabels()
    {
        if (_orientationLabels != null)
        {
            _orientationLabels.Dispose();
            _orientationLabels = null;
            RequestRender(); 
        }
    }

    private void HookActor(vtkRenderer renderer, VtkElementViewModel viewModel)
    {
        renderer.AddActor(viewModel.Actor);
        viewModel.Modified += OnSceneObjectsModified;
    }

    private void UnHookActor(vtkRenderer renderer, VtkElementViewModel viewModel)
    {
        renderer.RemoveActor(viewModel.Actor);
        viewModel.Modified -= OnSceneObjectsModified;
    }

    // internal render request
    private void RequestRender()
    {
        if (_isLoaded)
            OnSceneObjectsModified(this, EventArgs.Empty);
        else
            Dispatcher.InvokeAsync(() => OnSceneObjectsModified(this, EventArgs.Empty), DispatcherPriority.Loaded);
    }

    /// <summary>
    ///     Event handler. Render scene when the Modified event in <see cref="VtkElementViewModel" /> is raised.
    /// </summary>
    private void OnSceneObjectsModified(object? sender, EventArgs args)
    {
        if (!_isLoaded) return;

        // Only re-aim if the reference slice itself changed
        if (sender == _referenceSlice && _referenceSlice != null)
        {
            FitObliqueSlice(
                MainRenderer.GetActiveCamera(),
                _referenceSlice.Actor,
                _referenceSlice.PlaneNormal,
                _referenceSlice.PlaneAxisV,
                resetParallelScale: false); // preserve camera zoom-in state
        }

        MainRenderer.ResetCameraClippingRange();
        RenderWindowControl.RenderWindow.Render();
    }
    
    // ---------- dispose resources ----------------------
    public void Dispose()
    {
        _orientationCube?.Dispose();

        if (SceneObjects is { } objects)
        {
            foreach (VtkElementViewModel sceneObj in objects)
                sceneObj.Modified -= OnSceneObjectsModified;
        }

        if (OverlayObjects is { } overlays)
        {
            foreach (VtkElementViewModel overlayObj in overlays)
                overlayObj.Modified -= OnSceneObjectsModified;
        }

        WFHost.Child = null;
        WFHost?.Dispose();
        RenderWindowControl.Dispose();
        MainRenderer.Dispose();
        OverlayRenderer.Dispose();
    }



    #region Scene objects changed

    private static void OnImageObjectsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        VtkObliqueImageSceneControl control = (VtkObliqueImageSceneControl)d;
        control.RehookImageObjects((IReadOnlyList<ImageSliceViewModel>)e.OldValue, (IReadOnlyList<ImageSliceViewModel>)e.NewValue);
    }

    private void RehookImageObjects(
        IReadOnlyList<ImageSliceViewModel>? oldSceneObjects,
        IReadOnlyList<ImageSliceViewModel>? newSceneObjects)
    {
        // ----- detach old -----
        if (oldSceneObjects != null)
        {
            foreach (ImageSliceViewModel item in oldSceneObjects) UnHookActor(MainRenderer, item);
        }

        //  bail if empty ------------------------
        if (newSceneObjects == null || newSceneObjects.Count == 0)
        {
            _referenceSlice = null;
            return;
        }

        // ----- attach new -----
        foreach (ImageSliceViewModel item in newSceneObjects) HookActor(MainRenderer, item);

        // choose the slice that defines the view
        _referenceSlice = newSceneObjects[0];

        // initial camera placement
        FitObliqueSlice(
            MainRenderer.GetActiveCamera(),
            _referenceSlice.Actor,
            _referenceSlice.PlaneNormal,
            _referenceSlice.PlaneAxisV,
            resetParallelScale: true);

        // ----- render the scene to show the new stuff-----
        RequestRender();
    }


    private static void FitObliqueSlice(
        vtkCamera cam,
        vtkProp sliceActor,
        Vector3 normal, // = vm.PlaneNormal
        Vector3 vAxis, // = vm.PlaneAxisV  (camera “up”)
        bool resetParallelScale = true)
    {
        double[] b = sliceActor.GetBounds(); // world AABB
        double cx = 0.5 * (b[0] + b[1]);
        double cy = 0.5 * (b[2] + b[3]);
        double cz = 0.5 * (b[4] + b[5]);
        double w = b[1] - b[0];
        double h = b[3] - b[2];

        normal = Vector3.Normalize(normal);
        vAxis = Vector3.Normalize(vAxis);

        cam.ParallelProjectionOn();
        cam.SetFocalPoint(cx, cy, cz);
        cam.SetPosition(cx - normal.X * CamDist,
            cy - normal.Y * CamDist,
            cz - normal.Z * CamDist);
        cam.SetViewUp(vAxis.X, vAxis.Y, vAxis.Z);
        cam.SetClippingRange(0.1, 5000);
        if (resetParallelScale)
        {
            cam.SetParallelScale(0.5 * Math.Max(w, h));
        }
    }

    #endregion

    #region Overlay objects changed

    private static void OnOverlayObjectsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        VtkObliqueImageSceneControl control = (VtkObliqueImageSceneControl)d;
        control.UpdateOverlays((IReadOnlyList<VtkElementViewModel>)e.OldValue, (IReadOnlyList<VtkElementViewModel>)e.NewValue);
    }

    private void UpdateOverlays(
        IReadOnlyList<VtkElementViewModel>? oldOverlayObjects,
        IReadOnlyList<VtkElementViewModel>? newOverlayObjects)
    {
        if (oldOverlayObjects != null)
        {
            foreach (VtkElementViewModel item in oldOverlayObjects)
                UnHookActor(OverlayRenderer, item);
        }

        if (newOverlayObjects == null || newOverlayObjects.Count == 0)
            return;

        foreach (VtkElementViewModel item in newOverlayObjects)
            HookActor(OverlayRenderer, item);

        // ----- 3. Render the scene to show the new stuff-----
        RequestRender();
    }

    #endregion
}