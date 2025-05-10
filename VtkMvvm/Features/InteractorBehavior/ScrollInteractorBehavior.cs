using System.Reactive.Linq;
using System.Reactive.Subjects;
using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;

public sealed class ScrollInteractorBehavior : IInteractorBehavior, IDisposable
{
    private readonly Subject<bool> _scrollSubject = new();
    private vtkInteractorStyle? _style;
    
    
    /// <summary>
    /// Scroll forward emits true, scroll backward emits false
    /// </summary>
    public IObservable<bool> Scrolls => _scrollSubject.AsObservable();
    
    /// <summary>
    /// If not override, will forward the base event handler
    /// </summary>
    public bool OverrideBaseStyle { get; set; } = false;

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
        _scrollSubject.OnNext(false);
        if (OverrideBaseStyle) return;  // If override, don't call base style

        _style!.OnMouseWheelBackward();
    }

    private void OnScrollForward(vtkObject sender, vtkObjectEventArgs e)
    {
        _scrollSubject.OnNext(true);
        if (OverrideBaseStyle) return;  // If override, don't call base style

        _style!.OnMouseWheelForward();
    }
}