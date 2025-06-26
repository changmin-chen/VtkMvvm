using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Kitware.VTK;
using VtkMvvm.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace VtkMvvm.Controls;

/// <summary>
/// For binding to the image slice that may not be orthogonal.
/// </summary>
public partial class VtkObliqueImageSceneControl : UserControl, IDisposable
{
    public static readonly DependencyProperty SceneObjectsProperty = DependencyProperty.Register(
        nameof(SceneObjects),
        typeof(IList<VtkElementViewModel>),
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

    public void Dispose()
    {
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

    private void OnLoadedOnce(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedOnce;

        RenderWindowControl.RenderWindow.AddRenderer(MainRenderer);
        MainRenderer.SetBackground(0.0, 0.0, 0.0);

        // Render overlays onto the main renderer
        MainRenderer.SetLayer(0);
        OverlayRenderer.SetLayer(1);
        OverlayRenderer.InteractiveOff();
        OverlayRenderer.SetActiveCamera(MainRenderer.GetActiveCamera()); // keep cameras in sync
        RenderWindowControl.RenderWindow.SetNumberOfLayers(2);
        RenderWindowControl.RenderWindow.AddRenderer(OverlayRenderer);

        _isLoaded = true;
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
        VtkObliqueImageSceneControl control = (VtkObliqueImageSceneControl)d;
        control.UpdateSlices((IList<VtkElementViewModel>)e.OldValue, (IList<VtkElementViewModel>)e.NewValue);
    }

    private void UpdateSlices(
        IList<VtkElementViewModel>? oldSceneObjects,
        IList<VtkElementViewModel>? newSceneObjects)
    {
        // ----- 1. Remove & unsubscribe old stuff -----
        if (oldSceneObjects != null)
        {
            foreach (VtkElementViewModel item in oldSceneObjects)
                UnHookActor(MainRenderer, item);
        }

        // Bail early if we have nothing new
        if (newSceneObjects == null || newSceneObjects.Count == 0)
        {
            return;
        }

        // ----- 2. Add & subscribe new stuff -----
        foreach (VtkElementViewModel item in newSceneObjects)
            HookActor(MainRenderer, item);

        // ----- 3. Camera magic (use the first slice as reference) -----
        var first = newSceneObjects[0];
        FitAxialSlice(first.Actor);

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
    /// Unlike <see cref="VtkImageSceneControl"/> that decide which two axes to measure.
    /// We assume axial slice orientation (XY live, Z flat), which can fit to the vtkImageReslice output.
    /// </summary>
    private void FitAxialSlice(vtkProp slice)
    {
        ArgumentNullException.ThrowIfNull(slice);

        const double camDist = 500;

        vtkCamera? cam = MainRenderer.GetActiveCamera();
        cam.ParallelProjectionOn(); // orthographic, no perspective
        cam.SetClippingRange(0.1, 5000);

        double[] b = slice.GetBounds(); // xmin xmax ymin ymax zmin zmax

        // Assume XY live, Z flat. 
        double width = b[1] - b[0]; // xmax - xmin
        double height = b[3] - b[2]; // ymax - ymin
        double cx = 0.5 * (b[0] + b[1]);
        double cy = 0.5 * (b[2] + b[3]);
        double cz = b[4]; // zmin == zmax
        cam.SetPosition(cx, cy, cz + camDist);
        cam.SetViewUp(0, -1, 0);

        cam.SetFocalPoint(cx, cy, cz);
        cam.SetParallelScale(0.5 * Math.Max(width, height)); // <= **key line**
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