using Kitware.VTK;

namespace VtkMvvm.Controls;

public interface IVtkSceneControl
{
    public vtkRenderer MainRenderer { get; }
    public vtkRenderer OverlayRenderer { get; }
}