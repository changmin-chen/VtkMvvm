using System.Reactive.Subjects;
using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;

// ════════════════════════════════════════════════════════════════════════════════
//  BaseForwarder — owns ALL native VTK hooks; publishes a "bus" of VtkEvent
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
///  Make a single “event bus” (BaseForwarder) that owns the VTK hooks.
///  Let <see cref="IInteractorBehavior"/> subscribe to the bus instead of VTK directly
/// </summary>
internal sealed class BaseForwarder : IObservable<VtkEvent>, IDisposable
{
    private readonly vtkInteractorStyle _style;
    private readonly Subject<VtkEvent> _bus = new();
    private readonly HashSet<VtkEventId> _swallow = new();

    // Keep strong refs so we can detach on Dispose
    private readonly vtkObject.vtkObjectEventHandler _mouseMove;
    private readonly vtkObject.vtkObjectEventHandler _leftDown;
    private readonly vtkObject.vtkObjectEventHandler _leftUp;
    private readonly vtkObject.vtkObjectEventHandler _rightDown;
    private readonly vtkObject.vtkObjectEventHandler _rightUp;
    private readonly vtkObject.vtkObjectEventHandler _middleDown;
    private readonly vtkObject.vtkObjectEventHandler _middleUp;
    private readonly vtkObject.vtkObjectEventHandler _wheelFwd;
    private readonly vtkObject.vtkObjectEventHandler _wheelBack;

    public BaseForwarder(vtkInteractorStyle style)
    {
        _style = style ?? throw new ArgumentNullException(nameof(style));

        // Build handlers once, attach, remember for detach
        _mouseMove = (_, _) => Publish(VtkEventId.MouseMove, () => style.OnMouseMove());
        _leftDown = (_, _) => Publish(VtkEventId.LeftDown, () => style.OnLeftButtonDown());
        _leftUp = (_, _) => Publish(VtkEventId.LeftUp, () => style.OnLeftButtonUp());
        _rightDown = (_, _) => Publish(VtkEventId.RightDown, () => style.OnRightButtonDown());
        _rightUp = (_, _) => Publish(VtkEventId.RightUp, () => style.OnRightButtonUp());
        _middleDown = (_, _) => Publish(VtkEventId.MiddleDown, () => style.OnMiddleButtonDown());
        _middleUp = (_, _) => Publish(VtkEventId.MiddleUp, () => style.OnMiddleButtonUp());
        _wheelFwd = (_, _) => Publish(VtkEventId.WheelForward, () => style.OnMouseWheelForward());
        _wheelBack = (_, _) => Publish(VtkEventId.WheelBackward, () => style.OnMouseWheelBackward());

        style.MouseMoveEvt += _mouseMove;
        style.LeftButtonPressEvt += _leftDown;
        style.LeftButtonReleaseEvt += _leftUp;
        style.RightButtonPressEvt += _rightDown;
        style.RightButtonReleaseEvt += _rightUp;
        style.MiddleButtonPressEvt += _middleDown;
        style.MiddleButtonReleaseEvt += _middleUp;
        style.MouseWheelForwardEvt += _wheelFwd;
        style.MouseWheelBackwardEvt += _wheelBack;
    }

    public IDisposable Subscribe(IObserver<VtkEvent> o) => _bus.Subscribe(o);

    public void AddSwallow(VtkEventId id) => _swallow.Add(id);

    private void Publish(VtkEventId id, Action forwardOnce)
    {
        var iren = _style.GetInteractor();
        _bus.OnNext(new VtkEvent(id, iren));
        if (!_swallow.Contains(id)) forwardOnce();
    }

    public void Dispose()
    {
        // Detach to avoid memory leaks / double events when builder discarded
        _style.MouseMoveEvt -= _mouseMove;
        _style.LeftButtonPressEvt -= _leftDown;
        _style.LeftButtonReleaseEvt -= _leftUp;
        _style.RightButtonPressEvt -= _rightDown;
        _style.RightButtonReleaseEvt -= _rightUp;
        _style.MiddleButtonPressEvt -= _middleDown;
        _style.MiddleButtonReleaseEvt -= _middleUp;
        _style.MouseWheelForwardEvt -= _wheelFwd;
        _style.MouseWheelBackwardEvt -= _wheelBack;
        _bus.Dispose();
    }
}