using System.ComponentModel;
using System.Windows;
using Kitware.VTK;
using VtkMvvm.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace VtkMvvm.Controls;

public partial class VtkSingleActorControl : UserControl
{
    public static readonly DependencyProperty SceneObjectProperty = DependencyProperty.Register(
        nameof(SceneObject), typeof(VtkElementViewModel), typeof(VtkSingleActorControl),
        new PropertyMetadata(null, OnElementChanged));

    private readonly vtkRenderer _mainRenderer;
    private readonly RenderWindowControl _renderWindowControl;

    public VtkSingleActorControl()
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

    public VtkElementViewModel SceneObject
    {
        get => (VtkElementViewModel)GetValue(SceneObjectProperty);
        set => SetValue(SceneObjectProperty, value);
    }

    public vtkRenderWindowInteractor GetInteractor()
    {
        return _renderWindowControl.RenderWindow.GetInteractor();
    }

    public vtkRenderer GetRenderer()
    {
        return _mainRenderer;
    }

    private static void OnElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (VtkSingleActorControl)d;
        control.UpdateElement((VtkElementViewModel)e.OldValue, (VtkElementViewModel)e.NewValue);
    }

    private void UpdateElement(VtkElementViewModel? oldElement, VtkElementViewModel? newElement)
    {
        if (oldElement != null)
        {
            _mainRenderer.RemoveActor(oldElement.Actor);
            oldElement.Modified -= OnElementModified;
        }

        if (newElement != null)
        {
            _mainRenderer.AddActor(newElement.Actor);
            newElement.Modified += OnElementModified;
        }
    }

    private void OnElementModified(object? sender, EventArgs e)
    {
        _mainRenderer.ResetCamera();
        _renderWindowControl.RenderWindow.Render();
    }
}