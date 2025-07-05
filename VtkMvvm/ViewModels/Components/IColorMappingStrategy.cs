using Kitware.VTK;

namespace VtkMvvm.ViewModels.Components;

public interface IColorMappingStrategy : IDisposable
{
    void Apply(vtkImageMapToColors cmap);

    void Update();
}