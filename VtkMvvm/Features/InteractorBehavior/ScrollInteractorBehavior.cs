using System.Reactive.Linq;
using System.Reactive.Subjects;
using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;

public sealed class ScrollInteractorBehavior : IInteractorBehavior, IDisposable
{
    private readonly Subject<bool> _scrollSubject = new();
    private vtkInteractorStyle? _style;

    public IObservable<bool> Scrolls => _scrollSubject.AsObservable();

    public void Dispose()
    {
        Detach();
        _scrollSubject.Dispose();
    }

    public void AttachTo(vtkInteractorStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        _style = style;
        _style.MouseWheelForwardEvt += OnScrollForward;
        _style.MouseWheelBackwardEvt += OnScrollBackward;
    }

    public void Detach()
    {
        if (_style is null) return;
        _style.MouseWheelForwardEvt -= OnScrollForward;
        _style.MouseWheelBackwardEvt -= OnScrollBackward;
        _style = null;
    }

    private void OnScrollBackward(vtkObject sender, vtkObjectEventArgs e)
    {
        _style!.OnMouseWheelBackward();
        _scrollSubject.OnNext(false);
    }

    private void OnScrollForward(vtkObject sender, vtkObjectEventArgs e)
    {
        _style!.OnMouseWheelForward();
        _scrollSubject.OnNext(true);
    }
}