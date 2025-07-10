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
    private readonly CompositeDisposable _d = new();

    private MouseInteractorBuilder(vtkRenderWindowInteractor iren, vtkInteractorStyle baseStyle)
    {
        _iren = iren;
        _bus = new BaseForwarder(baseStyle);
        _d.Add(_bus);

        iren.SetInteractorStyle(baseStyle);
        iren.Initialize();
    }

    public static MouseInteractorBuilder Create(vtkRenderWindowInteractor iren, vtkInteractorStyle baseStyle)
        => new(iren, baseStyle);

    // ────────────────────── Action‑flavour APIs (unchanged) ──────────────────────

    public MouseInteractorBuilder LeftMove(MousePosHandler h, KeyMask k = KeyMask.None, bool swallow = false)
        => AddMouseAction(TriggerMouseButton.Left, drag: false, h, k, swallow);

    public MouseInteractorBuilder LeftDrag(MousePosHandler h, KeyMask k = KeyMask.None, bool swallow = false)
        => AddMouseAction(TriggerMouseButton.Left, drag: true, h, k, swallow);

    public MouseInteractorBuilder RightMove(MousePosHandler h, KeyMask k = KeyMask.None, bool swallow = false)
        => AddMouseAction(TriggerMouseButton.Right, drag: false, h, k, swallow);

    public MouseInteractorBuilder RightDrag(MousePosHandler h, KeyMask k = KeyMask.None, bool swallow = false)
        => AddMouseAction(TriggerMouseButton.Right, drag: true, h, k, swallow);

    public MouseInteractorBuilder MiddleMove(MousePosHandler h, KeyMask k = KeyMask.None, bool swallow = false)
        => AddMouseAction(TriggerMouseButton.Middle, drag: false, h, k, swallow);

    public MouseInteractorBuilder MiddleDrag(MousePosHandler h, KeyMask k = KeyMask.None, bool swallow = false)
        => AddMouseAction(TriggerMouseButton.Middle, drag: true, h, k, swallow);

    public MouseInteractorBuilder Scroll(Action<bool> h, KeyMask k = KeyMask.None, bool swallow = true)
        => ScrollRx(obs => obs.Subscribe(h), k, swallow);

    // ─────────────────────────── Rx‑flavour APIs ────────────────────────────

    public MouseInteractorBuilder LeftMoveRx(Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask k = KeyMask.None,
        bool swallow = false)
        => AddMouseRx(TriggerMouseButton.Left, drag: false, sub, k, swallow);

    public MouseInteractorBuilder LeftDragRx(Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask k = KeyMask.None,
        bool swallow = false)
        => AddMouseRx(TriggerMouseButton.Left, drag: true, sub, k, swallow);

    public MouseInteractorBuilder RightMoveRx(Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask k = KeyMask.None,
        bool swallow = false)
        => AddMouseRx(TriggerMouseButton.Right, drag: false, sub, k, swallow);

    public MouseInteractorBuilder RightDragRx(Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask k = KeyMask.None,
        bool swallow = false)
        => AddMouseRx(TriggerMouseButton.Right, drag: true, sub, k, swallow);

    public MouseInteractorBuilder MiddleMoveRx(Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask k = KeyMask.None,
        bool swallow = false)
        => AddMouseRx(TriggerMouseButton.Middle, drag: false, sub, k, swallow);

    public MouseInteractorBuilder MiddleDragRx(Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask k = KeyMask.None,
        bool swallow = false)
        => AddMouseRx(TriggerMouseButton.Middle, drag: true, sub, k, swallow);

    public MouseInteractorBuilder ScrollRx(Func<IObservable<bool>, IDisposable> sub,
        KeyMask k = KeyMask.None,
        bool swallow = true)
    {
        if (swallow)
        {
            _bus.AddSwallow(VtkEventId.WheelForward);
            _bus.AddSwallow(VtkEventId.WheelBackward);
        }
        var beh = new ScrollInteractorBehavior(_bus);
        _d.Add(beh);
        _d.Add(sub(beh.Scrolls.Where(_ => k.IsSatisfied(_iren))));
        return this;
    }

    // ─────────────────────────────── Build ────────────────────────────────

    public IDisposable Build() => _d;

    // ────────────────────────── Internal helpers ──────────────────────────

    private MouseInteractorBuilder AddMouseAction(TriggerMouseButton btn,
        bool drag,
        MousePosHandler h,
        KeyMask k,
        bool swallow)
        => AddMouseRx(btn, drag, obs => obs.Subscribe(p => h(p.x, p.y)), k, swallow);

    private MouseInteractorBuilder AddMouseRx(TriggerMouseButton btn,
        bool drag,
        Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask k,
        bool swallow)
    {
        if (swallow)
        {
            _bus.AddSwallow(VtkEventId.MouseMove);
            _bus.AddSwallow(btn switch
            {
                TriggerMouseButton.Left => VtkEventId.LeftDown,
                TriggerMouseButton.Right => VtkEventId.RightDown,
                TriggerMouseButton.Middle => VtkEventId.MiddleDown,
                _ => throw new ArgumentOutOfRangeException()
            });
            _bus.AddSwallow(btn switch
            {
                TriggerMouseButton.Left => VtkEventId.LeftUp,
                TriggerMouseButton.Right => VtkEventId.RightUp,
                TriggerMouseButton.Middle => VtkEventId.MiddleUp,
                _ => throw new ArgumentOutOfRangeException()
            });
        }

        var beh = new MouseInteractorBehavior(_bus, btn);
        _d.Add(beh);
        IObservable<(int x, int y)> stream = beh.Moves.Where(_ => k.IsSatisfied(_iren));
        if (drag) stream = stream.Where(_ => beh.IsPressing);
        _d.Add(sub(stream));
        return this;
    }
}