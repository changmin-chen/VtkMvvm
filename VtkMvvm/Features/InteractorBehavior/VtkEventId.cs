using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;


/// <summary>Distinct VTK interaction events we care about.</summary>
internal enum VtkEventId
{
    MouseMove,
    LeftDown,
    LeftUp,
    RightDown,
    RightUp,
    MiddleDown,
    MiddleUp,
    WheelForward,
    WheelBackward
}

/// <summary>Strong‑typed event payload that flows through the bus.</summary>
internal readonly record struct VtkEvent(VtkEventId Id, vtkRenderWindowInteractor Iren);

