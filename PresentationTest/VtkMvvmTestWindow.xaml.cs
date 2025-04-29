using System.Windows;
using VtkMvvm.Controls;
using VtkMvvm.Features.InteractorStyle;
using VtkMvvm.Models;

namespace PresentationTest;

public partial class VtkMvvmTestWindow : Window
{
    private readonly Dictionary<FreeHandPickInteractorStyle, VtkImageOrthogonalSlicesControl>
        _irenToControl = new();

    private VtkMvvmTestWindowViewModel _vm;

    public VtkMvvmTestWindow()
    {
        InitializeComponent();
        Loaded += Setup_AxialControl;
    }

    private void Setup_AxialControl(object sender, RoutedEventArgs e)
    {
        InitializeFreehandInteractor(
            [AxialControl, CoronalControl, SagittalControl]);

        // Add another actor to the main renderer
        if (DataContext is not VtkMvvmTestWindowViewModel vm) throw new InvalidOperationException("Wrong binding");
        _vm = vm;
    }

    /// <summary>
    /// Each controls has their own instance of freehand interactor
    /// </summary>
    private void InitializeFreehandInteractor(IEnumerable<VtkImageOrthogonalSlicesControl> controls)
    {
        foreach (var control in controls)
        {
            // Add first prop for pick list, should ensure it is the background img actor
            var props = control.MainRenderer.GetViewProps();
            props.InitTraversal();
            var first = props.GetNextProp();

            var iren = control.RenderWindowControl.RenderWindow.GetInteractor();
            var renderWindow = control.RenderWindowControl.RenderWindow;
            var drawIren = new FreeHandPickInteractorStyle(renderWindow, control.MainRenderer, first);
            iren.SetInteractorStyle(drawIren);
            iren.Initialize();
            _irenToControl[drawIren] = control;

            drawIren.WorldPositionsCaptured += OnGetWorldCoordinates;
        }
    }

    private void OnGetWorldCoordinates(object? sender, WorldPositionsCapturedEventArgs e)
    {
        if (sender is not FreeHandPickInteractorStyle iren) return;
        var owner = _irenToControl[iren]; // AxialControl, CoronalControl, or SagittalControl

        if (owner == AxialControl)
        {
            _vm.PaintLabelMap(SliceOrientation.Axial, e.WorldPositions);
        }
        else if (owner == CoronalControl)
        {
            _vm.PaintLabelMap(SliceOrientation.Coronal, e.WorldPositions);
        }
        else if (owner == SagittalControl)
        {
            _vm.PaintLabelMap(SliceOrientation.Sagittal, e.WorldPositions);
        }

        AxialControl.RenderWindowControl.RenderWindow.Render();
        CoronalControl.RenderWindowControl.RenderWindow.Render();
        SagittalControl.RenderWindowControl.RenderWindow.Render();
    }
}