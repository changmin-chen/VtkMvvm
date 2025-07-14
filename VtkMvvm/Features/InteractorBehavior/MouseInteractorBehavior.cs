// ════════════════════════════════════════════════════════════════════════════════
//  Behaviour: mouse move / drag
// ════════════════════════════════════════════════════════════════════════════════


using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;

/// <summary>
///     Represents a behavior that allows interaction with a visual area in response to mouse clicks and movements.
/// </summary>
public sealed class MouseInteractorBehavior : IDisposable
{
    private readonly Subject<(int x, int y)> _moves = new();
    private readonly CompositeDisposable _d = new();

    /// <summary>True while the trigger button is pressed.</summary>
    public bool IsPressing { get; private set; }

    /// <summary>Stream of (x,y) pairs.  
    /// Fires once on *button-down* and on every subsequent <c>MouseMove</c>.</summary>
    public IObservable<(int x, int y)> Moves => _moves.AsObservable();

    public MouseInteractorBehavior(IObservable<VtkEvent> bus, TriggerMouseButton trigger)
    {
        // Update IsPressing based on down/up events
        _d.Add(bus
            .Where(e => IsButtonEventForTrigger(e.Id, trigger))
            .Subscribe(e =>
            {
                IsPressing = IsDown(e.Id);  
                
                if (IsDown(e.Id)) // fire extra Move sample on *DOWN*
                    _moves.OnNext(GetMousePosition(e.Iren));
            }));

        // Emit (x,y) for every MouseMove event
        _d.Add(bus
            .Where(e => e.Id == VtkEventId.MouseMove)
            .Select(e => GetMousePosition(e.Iren))
            .Subscribe(_moves));
    }

    // --------------------------------------------------------------------
    // helpers
    // --------------------------------------------------------------------
    private static bool IsDown(VtkEventId id) =>
        id is VtkEventId.LeftDown or VtkEventId.MiddleDown or VtkEventId.RightDown;

    /// <summary>
    /// Returns true if the given VTK event corresponds to the specified mouse trigger button.
    /// Used to detect when to update IsPressing or emit the first sample.
    /// </summary>
    private static bool IsButtonEventForTrigger(VtkEventId id, TriggerMouseButton trigger) =>
        (id, trigger) switch
        {
            (VtkEventId.LeftDown, TriggerMouseButton.Left)
                or (VtkEventId.LeftUp, TriggerMouseButton.Left)
                or (VtkEventId.RightDown, TriggerMouseButton.Right)
                or (VtkEventId.RightUp, TriggerMouseButton.Right)
                or (VtkEventId.MiddleDown, TriggerMouseButton.Middle)
                or (VtkEventId.MiddleUp, TriggerMouseButton.Middle) => true,
            _ => false
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int x, int y) GetMousePosition(vtkRenderWindowInteractor iren)
    {
        Span<int> xy = stackalloc int[2];
        unsafe
        {
            fixed (int* p = xy) iren.GetEventPosition((IntPtr)p);
        }
        return (xy[0], xy[1]);
    }

    public void Dispose()
    {
        _d.Dispose();
        _moves.Dispose();
    }
}