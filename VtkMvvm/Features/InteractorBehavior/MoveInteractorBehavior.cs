using System.Reactive.Linq;
using System.Reactive.Subjects;
using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;

/// <summary>
///     Represents a behavior that allows interaction with a visual area in response to mouse clicks and movements.
/// </summary>
public sealed class MoveInteractorBehavior(TriggerMouseButton triggerButton)
    : IInteractorBehavior, IDisposable
{
    private readonly Subject<(int x, int y)> _clickSubject = new();
    private bool _isMousePress;

    private vtkInteractorStyle? _style;

    /// <summary>
    ///     Exposes the click‐position stream as an IObservable.
    ///     Subscribers will see (x,y) whenever the chosen button is dragged or clicked.
    /// </summary>
    public IObservable<(int x, int y)> Moves
    {
        get => _clickSubject.AsObservable();
    }

    public void Dispose()
    {
        Detach();
        _clickSubject.Dispose();
    }

    public void AttachTo(vtkInteractorStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        _style = style;

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
        if (_style is null) return;
        _isMousePress = true;
        _style.MouseMoveEvt += OnMouseMove;
        PushEventPosition();
    }

    private void OnMouseMove(vtkObject sender, vtkObjectEventArgs e)
    {
        if (!_isMousePress) return;
        PushEventPosition();
    }


    private void OnButtonRelease(vtkObject sender, vtkObjectEventArgs e)
    {
        if (_style is null) return;
        _isMousePress = false;
        _style.MouseMoveEvt -= OnMouseMove;
    }

    private unsafe void PushEventPosition()
    {
        // Allocate 2 ints on the *stack*, not the heap.
        Span<int> pos = stackalloc int[2];

        // Get a raw pointer to the first element and hand it to VTK.
        fixed (int* p = pos)
        {
            _style!.GetInteractor().GetEventPosition((IntPtr)p);
        }

        _clickSubject.OnNext((pos[0], pos[1]));
    }
}