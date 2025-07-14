using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;


/// <summary>Distinct VTK interaction events we care about.</summary>
public enum VtkEventId
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
public readonly record struct VtkEvent(VtkEventId Id, vtkRenderWindowInteractor Iren);

