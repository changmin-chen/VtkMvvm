using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;

/// <summary>
///     When attached to VTK RenderWindow, handle the interactor event
/// </summary>
public interface IInteractorBehavior
{
    void AttachTo(vtkInteractorStyle style);

    void Detach();
}

