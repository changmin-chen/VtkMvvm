using System.ComponentModel;
using System.Numerics;
using System.Windows;
using System.Windows.Threading;
using Kitware.VTK;
using VtkMvvm.Controls.Plugins;
using VtkMvvm.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace VtkMvvm.Controls;

/// <summary>
/// For binding to the image slice that may not be orthogonal.
/// </summary>
public partial class VtkObliqueImageSceneControl : UserControl, IDisposable
{
    private ImageObliqueSliceViewModel? _referenceSlice;  // use first oblique slice as reference to place and rotate the camera

    // ---------- Plugins --------------------------------------- 
    private OrientationCubeBehavior? _orientationCube; // L,R,P,A,S,I labeled cube fixed at screen bottom-left corner 


    // --------------------------------------------------------- 

    public static readonly DependencyProperty SceneObjectsProperty = DependencyProperty.Register(
        nameof(SceneObjects),
        typeof(IList<ImageObliqueSliceViewModel>),
        typeof(VtkObliqueImageSceneControl),
        new PropertyMetadata(null, OnSceneObjectsChanged));

    public static readonly DependencyProperty OverlayObjectsProperty = DependencyProperty.Register(
        nameof(OverlayObjects),
        typeof(IList<VtkElementViewModel>),
        typeof(VtkObliqueImageSceneControl),
        new PropertyMetadata(null, OnOverlayObjectsChanged));

    /// <summary>
    ///     Indicates whether the control is loaded. Or else the RenderWindowControl.RenderWindow may be null.
    /// </summary>
    private bool _isLoaded;

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

        // Render overlays onto the main renderer
        MainRenderer.SetLayer(0);
        OverlayRenderer.SetLayer(1);
        OverlayRenderer.PreserveDepthBufferOff();
        OverlayRenderer.InteractiveOff();
        OverlayRenderer.SetActiveCamera(MainRenderer.GetActiveCamera()); // keep cameras in sync
        renderWindow.SetNumberOfLayers(2);
        renderWindow.AddRenderer(OverlayRenderer);

        // ── orientation cube ───────────────────────────────
        _orientationCube = new OrientationCubeBehavior(renderWindow);

        _isLoaded = true;
    }

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


    public IList<VtkElementViewModel>? SceneObjects
    {
        get => (IList<VtkElementViewModel>)GetValue(SceneObjectsProperty);
        set => SetValue(SceneObjectsProperty, value);
    }

    public IList<VtkElementViewModel>? OverlayObjects
    {
        get => (IList<VtkElementViewModel>)GetValue(OverlayObjectsProperty);
        set => SetValue(OverlayObjectsProperty, value);
    }

    public vtkRenderer MainRenderer { get; } = vtkRenderer.New();
    public vtkRenderer OverlayRenderer { get; } = vtkRenderer.New();
    public RenderWindowControl RenderWindowControl { get; } = new();
    public vtkCamera Camera => MainRenderer.GetActiveCamera();


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
            Dispatcher.InvokeAsync(() => OnSceneObjectsModified(this, EventArgs.Empty),
                DispatcherPriority.Loaded);
    }

    /// <summary>
    ///     Render the scene when the actors are modified.
    ///     Hook onto the Modified event of the binding <see cref="VtkElementViewModel" />
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
                _referenceSlice.PlaneAxisV);
        }

        MainRenderer.ResetCameraClippingRange();
        RenderWindowControl.RenderWindow.Render();
    }


    #region Scene objects changed

    private static void OnSceneObjectsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        VtkObliqueImageSceneControl control = (VtkObliqueImageSceneControl)d;
        control.UpdateSlices((IList<ImageObliqueSliceViewModel>)e.OldValue, (IList<ImageObliqueSliceViewModel>)e.NewValue);
    }

    private void UpdateSlices(
        IList<ImageObliqueSliceViewModel>? oldSceneObjects,
        IList<ImageObliqueSliceViewModel>? newSceneObjects)
    {
        // ----- detach old -----
        if (oldSceneObjects != null)
        {
            foreach (ImageObliqueSliceViewModel item in oldSceneObjects) UnHookActor(MainRenderer, item);
        }

        //  bail if empty ------------------------
        if (newSceneObjects == null || newSceneObjects.Count == 0)
        {
            _referenceSlice = null;
            return;
        }

        // ----- attach new -----
        foreach (ImageObliqueSliceViewModel item in newSceneObjects) HookActor(MainRenderer, item);

        // choose the slice that defines the view
        _referenceSlice = newSceneObjects[0];

        // initial camera placement
        FitObliqueSlice(
            MainRenderer.GetActiveCamera(),
            _referenceSlice.Actor,
            _referenceSlice.PlaneNormal,
            _referenceSlice.PlaneAxisV);

        // ----- render the scene to show the new stuff-----
        RequestRender();
    }

    private const double CamDist = 500; // mm

    private static void FitObliqueSlice(
        vtkCamera cam,
        vtkProp sliceActor,
        Vector3 normal, // = vm.PlaneNormal
        Vector3 vAxis) // = vm.PlaneAxisV  (camera “up”)
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
        cam.SetParallelScale(0.5 * Math.Max(w, h));
        cam.SetClippingRange(0.1, 5000);
    }

    #endregion

    #region Overlay objects changed

    private static void OnOverlayObjectsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        VtkObliqueImageSceneControl control = (VtkObliqueImageSceneControl)d;
        control.UpdateOverlays((IList<VtkElementViewModel>)e.OldValue, (IList<VtkElementViewModel>)e.NewValue);
    }

    private void UpdateOverlays(
        IList<VtkElementViewModel>? oldOverlayObjects,
        IList<VtkElementViewModel>? newOverlayObjects)
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