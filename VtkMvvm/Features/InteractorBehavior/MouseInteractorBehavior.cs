using System.Reactive.Linq;
using System.Reactive.Subjects;
using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;

/// <summary>
///     Represents a behavior that allows interaction with a visual area in response to mouse clicks and movements.
/// </summary>
public sealed class MouseInteractorBehavior(TriggerMouseButton triggerButton)
    : IInteractorBehavior, IDisposable
{
    private readonly Subject<(int x, int y)> _moveSubject = new();

    private vtkInteractorStyle? _style;

    /// <summary>
    ///     Exposes the click‐position stream as an IObservable.
    ///     Subscribers will see (x,y) whenever the chosen button is moving.
    /// </summary>
    public IObservable<(int x, int y)> Moves => _moveSubject.AsObservable();

    /// <summary>
    ///     Exposed bool for filtering Moves observable.
    /// </summary>
    public bool IsPressing { get; private set; }

    public void Dispose()
    {
        Detach();
        _moveSubject.Dispose();
    }

    public void AttachTo(vtkInteractorStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        _style = style;

        _style.MouseMoveEvt += OnMouseMove;
        switch (triggerButton)
        {
            case TriggerMouseButton.Left:
                _style.LeftButtonPressEvt += OnButtonDown;
                _style.LeftButtonReleaseEvt += OnButtonRelease;
                break;
            case TriggerMouseButton.Right:
                _style.RightButtonPressEvt += OnButtonDown;
                _style.RightButtonReleaseEvt += OnButtonRelease;
                break;
            case TriggerMouseButton.Middle:
                _style.MiddleButtonPressEvt += OnButtonDown;
                _style.MiddleButtonReleaseEvt += OnButtonRelease;
                break;
        }
    }

    public void Detach()
    {
        if (_style is null) return;

        _style.MouseMoveEvt -= OnMouseMove;
        switch (triggerButton)
        {
            case TriggerMouseButton.Left:
                _style.LeftButtonPressEvt -= OnButtonDown;
                _style.LeftButtonReleaseEvt -= OnButtonRelease;
                break;
            case TriggerMouseButton.Right:
                _style.RightButtonPressEvt -= OnButtonDown;
                _style.RightButtonReleaseEvt -= OnButtonRelease;
                break;
            case TriggerMouseButton.Middle:
                _style.MiddleButtonPressEvt -= OnButtonDown;
                _style.MiddleButtonReleaseEvt -= OnButtonRelease;
                break;
        }

        _style = null;
    }

    private void OnButtonDown(vtkObject sender, vtkObjectEventArgs e)
    {
        switch (triggerButton)
        {
            case TriggerMouseButton.Left:
                _style!.OnLeftButtonDown();
                break;
            case TriggerMouseButton.Right:
                _style!.OnRightButtonDown();
                break;
            case TriggerMouseButton.Middle:
                _style!.OnMiddleButtonDown();
                break;
        }

        IsPressing = true;
        PushEventPosition();
    }

    private void OnMouseMove(vtkObject sender, vtkObjectEventArgs e)
    {
        _style!.OnMouseMove();

        PushEventPosition();
    }

    private void OnButtonRelease(vtkObject sender, vtkObjectEventArgs e)
    {
        switch (triggerButton)
        {
            case TriggerMouseButton.Left:
                _style!.OnLeftButtonUp();
                break;
            case TriggerMouseButton.Right:
                _style!.OnRightButtonUp();
                break;
            case TriggerMouseButton.Middle:
                _style!.OnMiddleButtonUp();
                break;
        }

        IsPressing = false;
    }

    private unsafe void PushEventPosition()
    {
        Span<int> pos = stackalloc int[2];
        fixed (int* p = pos)
        {
            _style!.GetInteractor().GetEventPosition((IntPtr)p);
        }

        _moveSubject.OnNext((pos[0], pos[1]));
    }
}