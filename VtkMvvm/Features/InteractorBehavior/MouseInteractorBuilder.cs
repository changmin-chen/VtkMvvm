// ────────────────────────────────────────────────────────────────────────────
// MouseInteractorBuilder.cs
// Fluent builder that wires VTK mouse / scroll events to Rx streams *or*
// plain delegates and hands back ONE IDisposable for clean-up.
// ────────────────────────────────────────────────────────────────────────────

using System.Reactive.Disposables;
using System.Reactive.Linq;
using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;

#region Public delegates / enums

/// <summary>Delegate for simple (x,y) mouse callbacks.</summary>
public delegate void MousePosHandler(int x, int y);

#endregion

/// <summary>
///     Fluent, functional helper that attaches <see cref="IInteractorBehavior"/>
///     instances to a <see cref="vtkRenderWindowInteractor"/> and returns
///     **one** <see cref="IDisposable"/> that tears everything down.
///     <para/>
///     Two flavours of API are exposed:
///     <list type="bullet">
///         <item><description><b>Rx-style</b> – you receive the raw <c>IObservable</c>.</description></item>
///         <item><description><b>Action-style</b> – you pass an <see cref="Action"/> or
///             <see cref="MousePosHandler"/>, and the builder hides the subscription boiler-plate.</description></item>
///     </list>
/// </summary>
public sealed class MouseInteractorBuilder
{
    private readonly vtkRenderWindowInteractor _iren;
    private readonly BaseForwarder _bus;
    private readonly CompositeDisposable _disposables = new();

    private MouseInteractorBuilder(vtkRenderWindowInteractor interactor, vtkInteractorStyle baseInteractorStyle)
    {
        _iren = interactor;
        _bus = new BaseForwarder(baseInteractorStyle);
        _disposables.Add(_bus);

        interactor.SetInteractorStyle(baseInteractorStyle);
        interactor.Initialize();
    }

    public static MouseInteractorBuilder Create(vtkRenderWindowInteractor interactor, vtkInteractorStyle baseInteractorStyle)
        => new(interactor, baseInteractorStyle);

    // ────────────────────── Action‑flavour APIs (unchanged) ──────────────────────

    public MouseInteractorBuilder LeftMove(MousePosHandler handler, KeyModifier key = KeyModifier.None, bool swallowEvent = false)
        => AddMouseAction(TriggerMouseButton.Left, isDrag: false, handler, key, swallowEvent);

    public MouseInteractorBuilder LeftDrag(MousePosHandler handler, KeyModifier key = KeyModifier.None, bool swallowEvent = false)
        => AddMouseAction(TriggerMouseButton.Left, isDrag: true, handler, key, swallowEvent);

    public MouseInteractorBuilder RightMove(MousePosHandler handler, KeyModifier key = KeyModifier.None, bool swallowEvent = false)
        => AddMouseAction(TriggerMouseButton.Right, isDrag: false, handler, key, swallowEvent);

    public MouseInteractorBuilder RightDrag(MousePosHandler handler, KeyModifier key = KeyModifier.None, bool swallowEvent = false)
        => AddMouseAction(TriggerMouseButton.Right, isDrag: true, handler, key, swallowEvent);

    public MouseInteractorBuilder MiddleMove(MousePosHandler handler, KeyModifier key = KeyModifier.None, bool swallowEvent = false)
        => AddMouseAction(TriggerMouseButton.Middle, isDrag: false, handler, key, swallowEvent);

    public MouseInteractorBuilder MiddleDrag(MousePosHandler handler, KeyModifier key = KeyModifier.None, bool swallowEvent = false)
        => AddMouseAction(TriggerMouseButton.Middle, isDrag: true, handler, key, swallowEvent);

    public MouseInteractorBuilder Scroll(Action<bool> handler, KeyModifier key = KeyModifier.None, bool swallowEvent = true)
        => ScrollRx(observable => observable.Subscribe(handler), key, swallowEvent);

    // ─────────────────────────── Rx‑flavour APIs ────────────────────────────

    public MouseInteractorBuilder LeftMoveRx(Func<IObservable<(int x, int y)>, IDisposable> subscriptionFactory,
        KeyModifier key = KeyModifier.None,
        bool swallowEvent = false)
        => AddMouseRx(TriggerMouseButton.Left, isDrag: false, subscriptionFactory, key, swallowEvent);

    public MouseInteractorBuilder LeftDragRx(Func<IObservable<(int x, int y)>, IDisposable> subscriptionFactory,
        KeyModifier key = KeyModifier.None,
        bool swallowEvent = false)
        => AddMouseRx(TriggerMouseButton.Left, isDrag: true, subscriptionFactory, key, swallowEvent);

    public MouseInteractorBuilder RightMoveRx(Func<IObservable<(int x, int y)>, IDisposable> subscriptionFactory,
        KeyModifier key = KeyModifier.None,
        bool swallowEvent = false)
        => AddMouseRx(TriggerMouseButton.Right, isDrag: false, subscriptionFactory, key, swallowEvent);

    public MouseInteractorBuilder RightDragRx(Func<IObservable<(int x, int y)>, IDisposable> subscriptionFactory,
        KeyModifier key = KeyModifier.None,
        bool swallowEvent = false)
        => AddMouseRx(TriggerMouseButton.Right, isDrag: true, subscriptionFactory, key, swallowEvent);

    public MouseInteractorBuilder MiddleMoveRx(Func<IObservable<(int x, int y)>, IDisposable> subscriptionFactory,
        KeyModifier key = KeyModifier.None,
        bool swallowEvent = false)
        => AddMouseRx(TriggerMouseButton.Middle, isDrag: false, subscriptionFactory, key, swallowEvent);

    public MouseInteractorBuilder MiddleDragRx(Func<IObservable<(int x, int y)>, IDisposable> subscriptionFactory,
        KeyModifier key = KeyModifier.None,
        bool swallowEvent = false)
        => AddMouseRx(TriggerMouseButton.Middle, isDrag: true, subscriptionFactory, key, swallowEvent);

    public MouseInteractorBuilder ScrollRx(Func<IObservable<bool>, IDisposable> subscriptionFactory,
        KeyModifier key = KeyModifier.None,
        bool swallowEvent = true)
    {
        if (swallowEvent)
        {
            _bus.AddSwallow(VtkEventId.WheelForward);
            _bus.AddSwallow(VtkEventId.WheelBackward);
        }
        var beh = new ScrollInteractorBehavior(_bus);
        _disposables.Add(beh);
        _disposables.Add(subscriptionFactory(beh.Scrolls.Where(_ => key.IsSatisfied(_iren))));
        return this;
    }

    // ─────────────────────────────── Build ────────────────────────────────

    public IDisposable Build() => _disposables;

    // ────────────────────────── Internal helpers ──────────────────────────

    private MouseInteractorBuilder AddMouseAction(TriggerMouseButton mouseButton,
        bool isDrag,
        MousePosHandler handler,
        KeyModifier key,
        bool swallowEvent)
        => AddMouseRx(mouseButton, isDrag, observable => observable.Subscribe(p => handler(p.x, p.y)), key, swallowEvent);

    private MouseInteractorBuilder AddMouseRx(TriggerMouseButton mouseButton,
        bool isDrag,
        Func<IObservable<(int x, int y)>, IDisposable> subscriptionFactory,
        KeyModifier key,
        bool swallowEvent)
    {
        if (swallowEvent)
        {
            _bus.AddSwallow(VtkEventId.MouseMove);
            _bus.AddSwallow(mouseButton switch
            {
                TriggerMouseButton.Left => VtkEventId.LeftDown,
                TriggerMouseButton.Right => VtkEventId.RightDown,
                TriggerMouseButton.Middle => VtkEventId.MiddleDown,
                _ => throw new ArgumentOutOfRangeException(nameof(mouseButton), mouseButton, null)
            });
            _bus.AddSwallow(mouseButton switch
            {
                TriggerMouseButton.Left => VtkEventId.LeftUp,
                TriggerMouseButton.Right => VtkEventId.RightUp,
                TriggerMouseButton.Middle => VtkEventId.MiddleUp,
                _ => throw new ArgumentOutOfRangeException(nameof(mouseButton), mouseButton, null)
            });
        }

        var beh = new MouseInteractorBehavior(_bus, mouseButton);
        _disposables.Add(beh);
        
        // Filter the stream by checking whether the mouse button is pressing and keymask
        IObservable<(int x, int y)> stream = beh.Moves.Where(_ => key.IsSatisfied(_iren));
        if (isDrag) stream = stream.Where(_ => beh.IsPressing);
        
        _disposables.Add(subscriptionFactory(stream));
        return this;
    }
}