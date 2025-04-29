using System.ComponentModel;
using System.Windows;
using Kitware.VTK;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace VtkMvvm.Controls;

public partial class VtkImageOrthogonalSliceControl : UserControl
{
    public static readonly DependencyProperty SceneObjectProperty = DependencyProperty.Register(
        nameof(SceneObject), typeof(ImageOrthogonalSliceViewModel), typeof(VtkImageOrthogonalSliceControl),
        new PropertyMetadata(null, OnElementChanged));

    public vtkRenderer MainRenderer { get; }
    public RenderWindowControl RenderWindowControl { get; }

    public VtkImageOrthogonalSliceControl()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this)) return;

        RenderWindowControl = new RenderWindowControl();
        RenderWindowControl.Dock = DockStyle.Fill;
        WFHost.Child = RenderWindowControl;

        MainRenderer = vtkRenderer.New();
        MainRenderer.GetActiveCamera().ParallelProjectionOn();

        Loaded += (sender, args) =>
        {
            RenderWindowControl.RenderWindow.AddRenderer(MainRenderer);
            MainRenderer.SetBackground(0.1, 0.1, 0.1);
        };
    }

    public ImageOrthogonalSliceViewModel SceneObject
    {
        get => (ImageOrthogonalSliceViewModel)GetValue(SceneObjectProperty);
        set => SetValue(SceneObjectProperty, value);
    }


    private static void OnElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (VtkImageOrthogonalSliceControl)d;
        control.UpdateElement((ImageOrthogonalSliceViewModel)e.OldValue, (ImageOrthogonalSliceViewModel)e.NewValue);
    }

    private void UpdateElement(ImageOrthogonalSliceViewModel? oldElement, ImageOrthogonalSliceViewModel? newElement)
    {
        if (oldElement != null)
        {
            MainRenderer.RemoveActor(oldElement.Actor);
            oldElement.Modified -= OnElementModified;
        }

        if (newElement != null)
        {
            MainRenderer.AddActor(newElement.Actor);
            newElement.Modified += OnElementModified;

            var camera = MainRenderer.GetActiveCamera();
            var center = newElement.ImageModel.Center;

            MainRenderer.ResetCamera();
            switch (newElement.Orientation)
            {
                case SliceOrientation.Axial:
                    camera.SetPosition(center[0], center[1], center[2] + 500.0);
                    camera.SetViewUp(0, -1, 0);
                    break;
                case SliceOrientation.Coronal:
                    camera.SetPosition(center[0], center[1] + 500.0, center[2]);
                    camera.SetViewUp(0, 0, 1);
                    break;
                case SliceOrientation.Sagittal:
                    camera.SetPosition(center[0] + 500.0, center[1], center[2]);
                    camera.SetViewUp(0, 0, 1);
                    break;
            }

            camera.SetFocalPoint(center[0], center[1], center[2]);
            MainRenderer.ResetCameraClippingRange();
        }
    }

    private void OnElementModified(object? sender, EventArgs e)
    {
        MainRenderer.ResetCameraClippingRange();
        RenderWindowControl.RenderWindow.Render();
    }
}