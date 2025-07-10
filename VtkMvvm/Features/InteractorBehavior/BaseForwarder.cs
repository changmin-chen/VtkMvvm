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

    public BaseForwarder(vtkInteractorStyle style)
    {
        _style = style ?? throw new ArgumentNullException(nameof(style));

        style.MouseMoveEvt += (_, _) => Publish(VtkEventId.MouseMove, () => style.OnMouseMove());
        style.LeftButtonPressEvt += (_, _) => Publish(VtkEventId.LeftDown, () => style.OnLeftButtonDown());
        style.LeftButtonReleaseEvt += (_, _) => Publish(VtkEventId.LeftUp, () => style.OnLeftButtonUp());
        style.RightButtonPressEvt += (_, _) => Publish(VtkEventId.RightDown, () => style.OnRightButtonDown());
        style.RightButtonReleaseEvt += (_, _) => Publish(VtkEventId.RightUp, () => style.OnRightButtonUp());
        style.MiddleButtonPressEvt += (_, _) => Publish(VtkEventId.MiddleDown, () => style.OnMiddleButtonDown());
        style.MiddleButtonReleaseEvt += (_, _) => Publish(VtkEventId.MiddleUp, () => style.OnMiddleButtonUp());
        style.MouseWheelForwardEvt += (_, _) => Publish(VtkEventId.WheelForward, () => style.OnMouseWheelForward());
        style.MouseWheelBackwardEvt += (_, _) => Publish(VtkEventId.WheelBackward, () => style.OnMouseWheelBackward());
    }

    public IDisposable Subscribe(IObserver<VtkEvent> observer) => _bus.Subscribe(observer);

    /// <summary>Prevent a particular event from being forwarded to the native base style.</summary>
    public void AddSwallow(VtkEventId evt) => _swallow.Add(evt);

    private void Publish(VtkEventId id, Action forwardOnce)
    {
        // Push through the bus first (behaviours will listen).
        var iren = _style.GetInteractor();
        _bus.OnNext(new VtkEvent(id, iren));

        if (!_swallow.Contains(id))
            forwardOnce(); // exactly once per physical VTK event
    }

    public void Dispose() => _bus.Dispose();
}