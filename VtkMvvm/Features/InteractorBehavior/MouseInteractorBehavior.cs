using System.Reactive.Linq;
using System.Reactive.Subjects;
using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;

/// <summary>
///     Represents a behavior that allows interaction with a visual area in response to mouse clicks and movements.
/// </summary>
public sealed class MouseInteractorBehavior
    : IInteractorBehavior, IDisposable
{
    private readonly TriggerMouseButton _triggerButton;
    private readonly Subject<(int x, int y)> _moveSubject = new();

    private vtkInteractorStyle? _style;

    public MouseInteractorBehavior(TriggerMouseButton triggerButton) 
    {
        _triggerButton = triggerButton;
    }

    /// <summary>
    ///     Exposes the click‐position stream as an IObservable.
    ///     Subscribers will see (x,y) whenever the chosen button is moving.
    /// </summary>
    public IObservable<(int x, int y)> Moves => _moveSubject.AsObservable();

    /// <summary>
    ///     Exposed bool for filtering Moves observable.
    /// </summary>
    public bool IsPressing { get; private set; }

    /// <summary>
    /// If not override, will forward the base event handler
    /// </summary>
    public bool OverrideBaseStyle { get; set; } = false;
    

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
        switch (_triggerButton)
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
        switch (_triggerButton)
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
        IsPressing = true;
        PushEventPosition();
        if (OverrideBaseStyle) return;  // not forward base style handler
        
        switch (_triggerButton)
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
    }

    private void OnMouseMove(vtkObject sender, vtkObjectEventArgs e)
    {
        PushEventPosition();
        if (OverrideBaseStyle) return;   // not forward base style handler
        
        _style!.OnMouseMove();
    }

    private void OnButtonRelease(vtkObject sender, vtkObjectEventArgs e)
    {
        IsPressing = false;
        if (OverrideBaseStyle) return;
        
        switch (_triggerButton)
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