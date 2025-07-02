using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Kitware.VTK;
using VtkMvvm.Controls.Plugins;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;
using VtkMvvm.ViewModels.Base;
using UserControl = System.Windows.Controls.UserControl;

namespace VtkMvvm.Controls;

/// <summary>
///     Displaying orthogonal slices of an image volume as the background image, while putting overlay objects onto it.
/// </summary>
public partial class VtkImageSceneControl : UserControl, IDisposable
{
    // ---------- Plugins --------------------------------------- 
    private OrientationLabelBehavior? _orientationLabels;  // L,R,P,A,S,I text labels on screen edges
    
    // --------------------------------------------------------- 

    public static readonly DependencyProperty SceneObjectsProperty = DependencyProperty.Register(
        nameof(SceneObjects),
        typeof(IReadOnlyList<ImageOrthogonalSliceViewModel>),
        typeof(VtkImageSceneControl),
        new PropertyMetadata(null, OnSceneObjectsChanged));

    public static readonly DependencyProperty OverlayObjectsProperty = DependencyProperty.Register(
        nameof(OverlayObjects),
        typeof(IReadOnlyList<VtkElementViewModel>),
        typeof(VtkImageSceneControl),
        new PropertyMetadata(null, OnOverlayObjectsChanged));

    /// <summary>
    ///     Indicates whether the control is loaded. Or else the RenderWindowControl.RenderWindow may be null.
    /// </summary>
    private bool _isLoaded;

    public VtkImageSceneControl()
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

        // ── orientation labels ───────────────────────────────
        _orientationLabels = new OrientationLabelBehavior(
            OverlayRenderer, // render layer 1
            MainRenderer.GetActiveCamera());

        _isLoaded = true;
    }


    public IReadOnlyList<ImageOrthogonalSliceViewModel>? SceneObjects
    {
        get => (IReadOnlyList<ImageOrthogonalSliceViewModel>)GetValue(SceneObjectsProperty);
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

    /// <summary>
    ///     Indicates the orientation of the slices so that the camera can be set up correctly.
    ///     This orientation is based on the first slice in the <see cref="SceneObjects"/> collection.
    /// </summary>
    public SliceOrientation Orientation { get; private set; }

    public void Dispose()
    {
        // ── dispose plugins ───────────────────────────────
        _orientationLabels?.Dispose();
        
        // ── dispose controls vtk components ───────────────────────────────
        if (SceneObjects is { } objects)
        {
            foreach (ImageOrthogonalSliceViewModel sceneObj in objects)
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

    /// <summary>
    ///     Render the scene when the actors are modified.
    ///     Hook onto the Modified event of the binding <see cref="VtkElementViewModel" />
    /// </summary>
    private void OnSceneObjectsModified(object? sender, EventArgs args)
    {
        if (!_isLoaded) return;

        MainRenderer.ResetCameraClippingRange();
        RenderWindowControl.RenderWindow.Render();
    }


    #region Scene objects changed

    private static void OnSceneObjectsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        VtkImageSceneControl control = (VtkImageSceneControl)d;
        control.UpdateSlices((IReadOnlyList<ImageOrthogonalSliceViewModel>)e.OldValue, (IReadOnlyList<ImageOrthogonalSliceViewModel>)e.NewValue);
    }

    private void UpdateSlices(
        IReadOnlyList<ImageOrthogonalSliceViewModel>? oldSceneObjects,
        IReadOnlyList<ImageOrthogonalSliceViewModel>? newSceneObjects)
    {
        // ----- 1. Remove & unsubscribe old stuff -----
        if (oldSceneObjects != null)
        {
            foreach (ImageOrthogonalSliceViewModel item in oldSceneObjects)
                UnHookActor(MainRenderer, item);
        }

        // Bail early if we have nothing new
        if (newSceneObjects == null || newSceneObjects.Count == 0)
        {
            return;
        }

        // ----- 2. Add & subscribe new stuff -----
        foreach (ImageOrthogonalSliceViewModel item in newSceneObjects)
            HookActor(MainRenderer, item);

        // ----- 3. Camera magic (use the first slice as reference) -----
        ImageOrthogonalSliceViewModel first = newSceneObjects[0];
        Orientation = first.Orientation;
        FitSlice(first.Actor, first.Orientation);

        // ----- 4. Render the scene to show the new stuff-----
        if (_isLoaded)
        {
            OnSceneObjectsModified(this, EventArgs.Empty);
        }
        else
        {
            Dispatcher.InvokeAsync(() => OnSceneObjectsModified(this, EventArgs.Empty), DispatcherPriority.Loaded);
        }
    }


    /// <summary>
    ///     Fits a single vtkImageActor / vtkImageSlice so it fills the viewport
    ///     without cropping, for any orthogonal orientation.
    /// </summary>
    private void FitSlice(vtkProp slice, SliceOrientation orient)
    {
        ArgumentNullException.ThrowIfNull(slice);

        const double camDist = 500;

        vtkCamera? cam = MainRenderer.GetActiveCamera();
        cam.ParallelProjectionOn(); // orthographic, no perspective
        cam.SetClippingRange(0.1, 5000);

        double[] b = slice.GetBounds(); // xmin xmax ymin ymax zmin zmax

        // Decide which two axes to measure
        double width, height, cx, cy, cz;

        switch (orient)
        {
            case SliceOrientation.Axial: // XY live, Z flat
                width = b[1] - b[0]; // xmax - xmin
                height = b[3] - b[2]; // ymax - ymin
                cx = 0.5 * (b[0] + b[1]);
                cy = 0.5 * (b[2] + b[3]);
                cz = b[4]; // zmin == zmax
                cam.SetPosition(cx, cy, cz - camDist); // feet → head (+Z)
                cam.SetViewUp(0, -1, 0); // anterior (-Y) to up
                break;

            case SliceOrientation.Coronal: // XZ live, Y flat
                width = b[1] - b[0]; // xmax - xmin
                height = b[5] - b[4]; // zmax - zmin
                cx = 0.5 * (b[0] + b[1]);
                cy = b[2]; // ymin == ymax
                cz = 0.5 * (b[4] + b[5]);
                cam.SetPosition(cx, cy - camDist, cz); // front → back (-Y)
                cam.SetViewUp(0, 0, 1); // superior (+Z) to up
                break;

            case SliceOrientation.Sagittal: // YZ live, X flat
                width = b[3] - b[2]; // ymax - ymin
                height = b[5] - b[4]; // zmax - zmin
                cx = b[0]; // xmin == xmax
                cy = 0.5 * (b[2] + b[3]);
                cz = 0.5 * (b[4] + b[5]);
                cam.SetPosition(cx - camDist, cy, cz); // left → right (-X)
                cam.SetViewUp(0, 0, 1); // superior (+Z) to up
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(orient));
        }

        cam.SetFocalPoint(cx, cy, cz);
        cam.SetParallelScale(0.5 * Math.Max(width, height)); // <= **key line**
    }

    #endregion

    #region Overlay objects changed

    private static void OnOverlayObjectsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        VtkImageSceneControl control = (VtkImageSceneControl)d;
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
        if (_isLoaded)
        {
            OnSceneObjectsModified(this, EventArgs.Empty);
        }
        else
        {
            Dispatcher.InvokeAsync(() => OnSceneObjectsModified(this, EventArgs.Empty), DispatcherPriority.Loaded);
        }
    }

    #endregion
}