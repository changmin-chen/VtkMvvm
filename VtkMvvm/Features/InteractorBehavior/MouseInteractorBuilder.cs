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
    // ───────────────────────── fields ─────────────────────────
    private readonly vtkRenderWindowInteractor _iren;
    private readonly vtkInteractorStyle _style;
    private readonly CompositeDisposable _disposables = new();
    private readonly List<IInteractorBehavior> _behaviours = new();

    // ─────────────────────── constructor ─────────────────────
    private MouseInteractorBuilder(vtkRenderWindowInteractor iren, vtkInteractorStyle baseStyle)
    {
        _iren = iren ?? throw new ArgumentNullException(nameof(iren));
        _style = baseStyle ?? throw new ArgumentNullException(nameof(baseStyle));
    }

    // ─────────────────────── factory ─────────────────────────
    public static MouseInteractorBuilder Create(vtkRenderWindowInteractor iren, vtkInteractorStyle baseStyle)
        => new(iren, baseStyle);

    // ═══════════════════════════════════════════════════════════════════════
    //  Rx-FLAVOUR  – caller handles the IObservable
    // ═══════════════════════════════════════════════════════════════════════

    #region Rx APIs

    public MouseInteractorBuilder LeftMoveRx(Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask keys = KeyMask.None,
        bool swallow = false)
        => AddMouse(TriggerMouseButton.Left, drag: false, sub, keys, swallow);

    public MouseInteractorBuilder LeftDragRx(Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask keys = KeyMask.None,
        bool swallow = false)
        => AddMouse(TriggerMouseButton.Left, drag: true, sub, keys, swallow);

    public MouseInteractorBuilder RightMoveRx(Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask keys = KeyMask.None,
        bool swallow = false)
        => AddMouse(TriggerMouseButton.Right, false, sub, keys, swallow);

    public MouseInteractorBuilder RightDragRx(Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask keys = KeyMask.None,
        bool swallow = false)
        => AddMouse(TriggerMouseButton.Right, true, sub, keys, swallow);

    public MouseInteractorBuilder MiddleMoveRx(Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask keys = KeyMask.None,
        bool swallow = false)
        => AddMouse(TriggerMouseButton.Middle, false, sub, keys, swallow);

    public MouseInteractorBuilder MiddleDragRx(Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask keys = KeyMask.None,
        bool swallow = false)
        => AddMouse(TriggerMouseButton.Middle, true, sub, keys, swallow);

    public MouseInteractorBuilder ScrollRx(Func<IObservable<bool>, IDisposable> sub,
        KeyMask keys = KeyMask.None,
        bool swallow = true)
    {
        var beh = new ScrollInteractorBehavior { OverrideBaseStyle = swallow };
        Plug(beh,
            _ => keys == KeyMask.None
                ? beh.Scrolls
                : beh.Scrolls.Where(_ => keys.IsSatisfied(_iren)),
            sub);
        return this;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    //  ACTION-FLAVOUR  – builder hides the Subscribe/Dispose dance
    // ═══════════════════════════════════════════════════════════════════════

    #region Action APIs

    public MouseInteractorBuilder LeftMove(MousePosHandler handler,
        KeyMask keys = KeyMask.None,
        bool swallow = false)
        => AddMouseAction(TriggerMouseButton.Left, drag: false, handler, keys, swallow);

    public MouseInteractorBuilder LeftDrag(MousePosHandler handler,
        KeyMask keys = KeyMask.None,
        bool swallow = false)
        => AddMouseAction(TriggerMouseButton.Left, true, handler, keys, swallow);

    public MouseInteractorBuilder RightMove(MousePosHandler handler,
        KeyMask keys = KeyMask.None,
        bool swallow = false)
        => AddMouseAction(TriggerMouseButton.Right, false, handler, keys, swallow);

    public MouseInteractorBuilder RightDrag(MousePosHandler handler,
        KeyMask keys = KeyMask.None,
        bool swallow = false)
        => AddMouseAction(TriggerMouseButton.Right, true, handler, keys, swallow);

    public MouseInteractorBuilder MiddleMove(MousePosHandler handler,
        KeyMask keys = KeyMask.None,
        bool swallow = false)
        => AddMouseAction(TriggerMouseButton.Middle, false, handler, keys, swallow);

    public MouseInteractorBuilder MiddleDrag(MousePosHandler handler,
        KeyMask keys = KeyMask.None,
        bool swallow = false)
        => AddMouseAction(TriggerMouseButton.Middle, true, handler, keys, swallow);

    public MouseInteractorBuilder Scroll(Action<bool> handler,
        KeyMask keys = KeyMask.None,
        bool swallow = true)
        => ScrollRx(obs => obs.Subscribe(handler), keys, swallow);

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    //  Build / Dispose
    // ═══════════════════════════════════════════════════════════════════════
    /// <returns>
    ///     A composite disposable that:
    ///     <list type="bullet">
    ///         <item><description>unsubscribes all Rx streams</description></item>
    ///         <item><description>detaches every <see cref="IInteractorBehavior"/></description></item>
    ///     </list>
    /// </returns>
    public IDisposable Build()
    {
        _iren.SetInteractorStyle(_style);
        _iren.Initialize();

        _disposables.Add(Disposable.Create(() =>
        {
            foreach (var b in _behaviours) b.Detach();
        }));
        return _disposables;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Internal helpers
    // ═══════════════════════════════════════════════════════════════════════

    #region Internal plumbing

    private MouseInteractorBuilder AddMouse(TriggerMouseButton btn,
        bool drag,
        Func<IObservable<(int x, int y)>, IDisposable> sub,
        KeyMask keys,
        bool swallow)
    {
        return AddMouseBehaviour(btn, drag, keys, sub, swallow);
    }

    private MouseInteractorBuilder AddMouseAction(TriggerMouseButton btn,
        bool drag,
        MousePosHandler handler,
        KeyMask keys,
        bool swallow)
    {
        // Wrap into Rx flavour internally
        return AddMouse(btn, drag,
            obs => obs.Subscribe(p => handler(p.x, p.y)),
            keys, swallow);
    }

    private MouseInteractorBuilder AddMouseBehaviour(TriggerMouseButton button,
        bool drag,
        KeyMask keys,
        Func<IObservable<(int x, int y)>, IDisposable> subscriber,
        bool swallow)
    {
        var beh = new MouseInteractorBehavior(button) { OverrideBaseStyle = swallow };

        IObservable<(int x, int y)> stream = beh.Moves;
        stream = stream.Where(_ => keys.IsSatisfied(_iren)); // key mask
        if (drag) stream = stream.Where(_ => beh.IsPressing); // pressing mask

        Plug(beh, _ => stream, subscriber);
        return this;
    }

    private void Plug<TStream>(IInteractorBehavior behaviour,
        Func<IInteractorBehavior, IObservable<TStream>> streamSelector,
        Func<IObservable<TStream>, IDisposable> subscriber)
    {
        behaviour.AttachTo(_style);
        _behaviours.Add(behaviour);
        _disposables.Add((IDisposable)behaviour); // dispose behaviour
        _disposables.Add(subscriber(streamSelector(behaviour))); // dispose Rx
    }

    #endregion
}