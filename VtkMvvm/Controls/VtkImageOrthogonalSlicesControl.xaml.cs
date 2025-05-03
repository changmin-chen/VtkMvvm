using System.ComponentModel;
using System.Windows;
using Kitware.VTK;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace VtkMvvm.Controls;

public partial class VtkImageOrthogonalSlicesControl : UserControl
{
    public static readonly DependencyProperty SceneObjectsProperty = DependencyProperty.Register(
        nameof(SceneObjects), typeof(IEnumerable<ImageOrthogonalSliceViewModel>), typeof(VtkImageOrthogonalSlicesControl),
        new PropertyMetadata(null, OnSceneObjectsChanged));

    public vtkRenderer MainRenderer { get; } = vtkRenderer.New();
    public RenderWindowControl RenderWindowControl { get; } = new();

    public IEnumerable<ImageOrthogonalSliceViewModel> SceneObjects
    {
        get => (IEnumerable<ImageOrthogonalSliceViewModel>)GetValue(SceneObjectsProperty);
        set => SetValue(SceneObjectsProperty, value);
    }


    private static void OnSceneObjectsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (VtkImageOrthogonalSlicesControl)d;
        control.UpdateSlices((IEnumerable<ImageOrthogonalSliceViewModel>)e.OldValue, (IEnumerable<ImageOrthogonalSliceViewModel>)e.NewValue);
    }

    private void UpdateSlices(
        IEnumerable<ImageOrthogonalSliceViewModel>? oldSceneObjects,
        IEnumerable<ImageOrthogonalSliceViewModel>? newSceneObjects)
    {
        // 1) Unsubscribe/remove old actors
        if (oldSceneObjects != null)
        {
            foreach (var sceneObject in oldSceneObjects)
            {
                MainRenderer.AddActor(sceneObject.Actor);
                sceneObject.Modified += OnSceneObjectModified;
            }
        }

        // 2) If nothing to add, bail out early
        if (newSceneObjects == null) return;
        var sceneList = newSceneObjects.ToList();
        if (sceneList.Count == 0) return;

        // 3) Add actors & subscribe handlers
        foreach (var sceneObject in sceneList)
        {
            MainRenderer.AddActor(sceneObject.Actor);
            sceneObject.Modified += OnSceneObjectModified;
        }

        var first = sceneList[0];
        var camera = MainRenderer.GetActiveCamera();
        var center = first.ImageModel.Center;

        MainRenderer.ResetCamera();
        switch (first.Orientation)
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

    private void OnSceneObjectModified(object? sender, EventArgs e)
    {
        MainRenderer.ResetCameraClippingRange();
        RenderWindowControl.RenderWindow.Render();
    }
}