// ════════════════════════════════════════════════════════════════════════════════
//  Behaviour: mouse move / drag
// ════════════════════════════════════════════════════════════════════════════════


using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;


/// <summary>
///     Represents a behavior that allows interaction with a visual area in response to mouse clicks and movements.
/// </summary>
public sealed class MouseInteractorBehavior : IDisposable
{
    private readonly Subject<(int x,int y)> _moves = new();
    private readonly CompositeDisposable _d = new();

    /// <summary>true while the trigger button is pressed.</summary>
    public bool IsPressing { get; private set; }

    public IObservable<(int x,int y)> Moves => _moves.AsObservable();

    public MouseInteractorBehavior(IObservable<VtkEvent> bus, TriggerMouseButton trigger)
    {
        // Update IsPressing based on down/up events
        _d.Add(bus.Subscribe(e =>
        {
            IsPressing = e.Id switch
            {
                VtkEventId.LeftDown   when trigger == TriggerMouseButton.Left   => true,
                VtkEventId.LeftUp     when trigger == TriggerMouseButton.Left   => false,
                VtkEventId.RightDown  when trigger == TriggerMouseButton.Right  => true,
                VtkEventId.RightUp    when trigger == TriggerMouseButton.Right  => false,
                VtkEventId.MiddleDown when trigger == TriggerMouseButton.Middle => true,
                VtkEventId.MiddleUp   when trigger == TriggerMouseButton.Middle => false,
                _ => IsPressing
            };
        }));

        // Emit (x,y) for every MouseMove event
        _d.Add(bus
            .Where(e => e.Id == VtkEventId.MouseMove)
            .Select(e => GetMousePos(e.Iren))
            .Subscribe(_moves));
    }

    private static (int x,int y) GetMousePos(vtkRenderWindowInteractor iren)
    {
        Span<int> xy = stackalloc int[2];
        unsafe { fixed(int* p = xy) iren.GetEventPosition((IntPtr)p); }
        return (xy[0], xy[1]);
    }

    public void Dispose() => _d.Dispose();
}