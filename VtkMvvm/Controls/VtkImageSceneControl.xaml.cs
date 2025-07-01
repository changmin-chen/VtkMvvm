using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Kitware.VTK;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace VtkMvvm.Controls;

public partial class VtkImageSceneControl : UserControl, IDisposable
{
    public static readonly DependencyProperty SceneObjectsProperty = DependencyProperty.Register(
        nameof(SceneObjects), typeof(IEnumerable<ImageOrthogonalSliceViewModel>), typeof(VtkImageSceneControl),
        new PropertyMetadata(null, OnSceneObjectsChanged));

    public static readonly DependencyProperty OverlayObjectsProperty = DependencyProperty.Register(
        nameof(OverlayObjects), typeof(IEnumerable<VtkElementViewModel>), typeof(VtkImageSceneControl),
        new PropertyMetadata(null, OnOverlayObjectsChanged));

    public VtkImageSceneControl()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this)) return;

        RenderWindowControl.Dock = DockStyle.Fill;
        WFHost.Child = RenderWindowControl;
        MainRenderer.GetActiveCamera().ParallelProjectionOn();

        Loaded += OnLoadedOnce;
    }

    public IEnumerable<ImageOrthogonalSliceViewModel>? SceneObjects
    {
        get => (IEnumerable<ImageOrthogonalSliceViewModel>)GetValue(SceneObjectsProperty);
        set => SetValue(SceneObjectsProperty, value);
    }

    public IEnumerable<VtkElementViewModel>? OverlayObjects
    {
        get => (IEnumerable<VtkElementViewModel>)GetValue(OverlayObjectsProperty);
        set => SetValue(OverlayObjectsProperty, value);
    }

    public vtkRenderer MainRenderer { get; } = vtkRenderer.New();
    public vtkRenderer OverlayRenderer { get; } = vtkRenderer.New();
    public RenderWindowControl RenderWindowControl { get; } = new();

    /// <summary>
    ///     Indicates whether the control is loaded. Or else the RenderWindowControl.RenderWindow may be null.
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    ///     Indicates the orientation of the slices so that the camera can be set up correctly.
    ///     This orientation is based on the first slice in the SceneObjects collection.
    /// </summary>
    public SliceOrientation Orientation { get; private set; }

    public void Dispose()
    {
        if (SceneObjects is { } objects)
        {
            foreach (ImageOrthogonalSliceViewModel sceneObj in objects)
            {
                sceneObj.Modified -= OnSceneObjectsModified;
            }
        }

        if (OverlayObjects is { } overlays)
        {
            foreach (VtkElementViewModel overlayObj in overlays)
            {
                overlayObj.Modified -= OnSceneObjectsModified;
            }
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

        IsLoaded = true;
    }

    public void SetInteractStyle(vtkInteractorObserver interactorStyle)
    {
        ArgumentNullException.ThrowIfNull(interactorStyle);

        vtkRenderWindowInteractor? iren = RenderWindowControl.RenderWindow.GetInteractor();
        iren.SetInteractorStyle(interactorStyle);
        iren.Initialize();
    }


    #region Scene objects changed

    private static void OnSceneObjectsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        VtkImageSceneControl control = (VtkImageSceneControl)d;
        control.UpdateSlices((IEnumerable<ImageOrthogonalSliceViewModel>)e.OldValue, (IEnumerable<ImageOrthogonalSliceViewModel>)e.NewValue);
    }

    private void UpdateSlices(
        IEnumerable<ImageOrthogonalSliceViewModel>? oldSceneObjects,
        IEnumerable<ImageOrthogonalSliceViewModel>? newSceneObjects)
    {
        // ----- 1. Remove & unsubscribe old stuff -----
        if (oldSceneObjects != null)
        {
            foreach (ImageOrthogonalSliceViewModel sceneObject in oldSceneObjects)
            {
                MainRenderer.RemoveActor(sceneObject.Actor);
                sceneObject.Modified -= OnSceneObjectsModified;
            }
        }

        // Bail early if we have nothing new
        if (newSceneObjects == null)
        {
            return;
        }

        // ----- 2. Add & subscribe new stuff -----
        ImageOrthogonalSliceViewModel[] array = newSceneObjects.ToArray();
        if (array.Length == 0)
        {
            return;
        }

        foreach (ImageOrthogonalSliceViewModel sceneObject in array)
        {
            MainRenderer.AddActor(sceneObject.Actor);
            sceneObject.Modified += OnSceneObjectsModified;
        }

        // ----- 3. Camera magic (use the first slice as reference) -----
        ImageOrthogonalSliceViewModel first = array[0];
        Orientation = first.Orientation;
        FitSlice(first.Actor, first.Orientation);

        // ----- 4. Render the scene to show the new stuff-----
        if (IsLoaded)
        {
            OnSceneObjectsModified(this, EventArgs.Empty);
        }
        else
        {
            Dispatcher.InvokeAsync(() => OnSceneObjectsModified(this, EventArgs.Empty), DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    ///     Render the scene when the actors are modified.
    ///     Hook onto the Modified event of the binding <see cref="VtkElementViewModel" />
    /// </summary>
    private void OnSceneObjectsModified(object? sender, EventArgs args)
    {
        if (IsLoaded)
        {
            MainRenderer.ResetCameraClippingRange();
            RenderWindowControl.RenderWindow.Render();
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
                cam.SetPosition(cx, cy, cz + camDist);
                cam.SetViewUp(0, -1, 0);
                break;

            case SliceOrientation.Coronal: // XZ live, Y flat
                width = b[1] - b[0]; // xmax - xmin
                height = b[5] - b[4]; // zmax - zmin
                cx = 0.5 * (b[0] + b[1]);
                cy = b[2]; // ymin == ymax
                cz = 0.5 * (b[4] + b[5]);
                cam.SetPosition(cx, cy + camDist, cz);
                cam.SetViewUp(0, 0, 1);
                break;

            case SliceOrientation.Sagittal: // YZ live, X flat
                width = b[3] - b[2]; // ymax - ymin
                height = b[5] - b[4]; // zmax - zmin
                cx = b[0]; // xmin == xmax
                cy = 0.5 * (b[2] + b[3]);
                cz = 0.5 * (b[4] + b[5]);
                cam.SetPosition(cx + camDist, cy, cz);
                cam.SetViewUp(0, 0, 1);
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
        control.UpdateOverlays((IEnumerable<VtkElementViewModel>)e.OldValue, (IEnumerable<VtkElementViewModel>)e.NewValue);
    }

    private void UpdateOverlays(
        IEnumerable<VtkElementViewModel>? oldOverlayObjects,
        IEnumerable<VtkElementViewModel>? newOverlayObjects)
    {
        if (oldOverlayObjects != null)
        {
            foreach (VtkElementViewModel overlayObject in oldOverlayObjects)
            {
                OverlayRenderer.RemoveActor(overlayObject.Actor);
                overlayObject.Modified -= OnSceneObjectsModified;
            }
        }

        if (newOverlayObjects == null)
        {
            return;
        }

        // ----- 2. Add & subscribe new stuff -----
        VtkElementViewModel[] array = newOverlayObjects.ToArray();
        if (array.Length == 0)
        {
            return;
        }

        foreach (VtkElementViewModel overlayObject in array)
        {
            OverlayRenderer.AddActor(overlayObject.Actor);
            overlayObject.Modified += OnSceneObjectsModified;
        }

        // ----- 3. Render the scene to show the new stuff-----
        if (IsLoaded)
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