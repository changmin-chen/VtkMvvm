using System.ComponentModel;
using System.Windows;
using Kitware.VTK;
using VtkMvvm.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace VtkMvvm.Controls;

public partial class VtkRenderControl : UserControl
{
    public static readonly DependencyProperty SceneObjectsProperty = DependencyProperty.Register(
        nameof(SceneObjects), typeof(IEnumerable<VtkElementViewModel>), typeof(VtkRenderControl),
        new PropertyMetadata(null, OnSceneObjectsChanged));

    public vtkRenderer MainRenderer { get; }
    public RenderWindowControl RenderWindowControl { get; }

    public VtkRenderControl()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this)) return;

        RenderWindowControl = new RenderWindowControl();
        RenderWindowControl.Dock = DockStyle.Fill;
        WFHost.Child = RenderWindowControl;

        MainRenderer = vtkRenderer.New();

        Loaded += (sender, args) =>
        {
            RenderWindowControl.RenderWindow.AddRenderer(MainRenderer);
            MainRenderer.SetBackground(0.1, 0.1, 0.1);
        };
    }

    public IEnumerable<VtkElementViewModel> SceneObjects
    {
        get => (IEnumerable<VtkElementViewModel>)GetValue(SceneObjectsProperty);
        set => SetValue(SceneObjectsProperty, value);
    }

    public vtkRenderWindowInteractor GetInteractor()
    {
        return RenderWindowControl.RenderWindow.GetInteractor();
    }

    private static void OnSceneObjectsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (VtkRenderControl)d;
        // create or remove actors corresponding to the view-model objects
        control.UpdateScene(e.OldValue as IEnumerable<VtkElementViewModel>,
            e.NewValue as IEnumerable<VtkElementViewModel>);
    }

    private void UpdateScene(IEnumerable<VtkElementViewModel>? oldSceneObjects,
        IEnumerable<VtkElementViewModel>? newSceneObjects)
    {
        if (oldSceneObjects != null)
        {
            foreach (var sceneObject in oldSceneObjects)
            {
                MainRenderer.AddActor(sceneObject.Actor);
                sceneObject.Modified += OnSceneObjectModified;
            }
        }

        if (newSceneObjects != null)
        {
            foreach (var sceneObject in newSceneObjects)
            {
                MainRenderer.AddActor(sceneObject.Actor);
                sceneObject.Modified += OnSceneObjectModified;
            }
        }
    }

    private void OnSceneObjectModified(object? sender, EventArgs e)
    {
        MainRenderer.ResetCamera(); // 針對顯示在變動但物件不變的情況下需要(e.g. SetDisplayExtent)。但是對物件變動但顯示位置不變就不需要(Reslice)
        RenderWindowControl.RenderWindow.Render();
    }
}