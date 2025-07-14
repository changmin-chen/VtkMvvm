// ════════════════════════════════════════════════════════════════════════════════
//  Behaviour: scroll wheel
// ════════════════════════════════════════════════════════════════════════════════

using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace VtkMvvm.Features.InteractorBehavior;

public sealed class ScrollInteractorBehavior : IDisposable
{
    private readonly Subject<bool> _scrolls = new();
    private readonly IDisposable _d;

    /// <summary>True = wheel forward, false = wheel backward.</summary>
    public IObservable<bool> Scrolls => _scrolls.AsObservable();

    public ScrollInteractorBehavior(IObservable<VtkEvent> bus)
    {
        _d = bus
            .Where(e => e.Id is VtkEventId.WheelForward or VtkEventId.WheelBackward)
            .Select(e => e.Id == VtkEventId.WheelForward)
            .Subscribe(_scrolls);
    }

    public void Dispose()
    {
        _d.Dispose();
        _scrolls.Dispose();
    }
}