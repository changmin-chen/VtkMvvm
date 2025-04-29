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

    private readonly vtkRenderer _mainRenderer;
    private readonly RenderWindowControl _renderWindowControl;

    public VtkRenderControl()
    {
        InitializeComponent();
        if (DesignerProperties.GetIsInDesignMode(this)) return;

        _renderWindowControl = new RenderWindowControl();
        _renderWindowControl.Dock = DockStyle.Fill;
        WFHost.Child = _renderWindowControl;

        _mainRenderer = vtkRenderer.New();

        Loaded += (sender, args) =>
        {
            _renderWindowControl.RenderWindow.AddRenderer(_mainRenderer);
            _mainRenderer.SetBackground(0.1, 0.1, 0.1);
        };
    }

    public IEnumerable<VtkElementViewModel> SceneObjects
    {
        get => (IEnumerable<VtkElementViewModel>)GetValue(SceneObjectsProperty);
        set => SetValue(SceneObjectsProperty, value);
    }

    public vtkRenderWindowInteractor GetInteractor()
    {
        return _renderWindowControl.RenderWindow.GetInteractor();
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
        if (oldSceneObjects is null && newSceneObjects is not null) // initial bind
        {
            foreach (var sceneObject in newSceneObjects)
            {
                _mainRenderer.AddActor(sceneObject.Actor);
                sceneObject.Modified += OnSceneObjectModified;
            }
        }
        // TODO: handle unbind/ collection change
    }

    private void OnSceneObjectModified(object? sender, EventArgs e)
    {
        _mainRenderer.ResetCamera(); // 針對顯示在變動但物件不變的情況下需要(e.g. SetDisplayExtent)。但是對物件變動但顯示位置不變就不需要(Reslice)
        _renderWindowControl.RenderWindow.Render();
    }
}