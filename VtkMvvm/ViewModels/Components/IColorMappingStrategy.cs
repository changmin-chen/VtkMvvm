using Kitware.VTK;

namespace VtkMvvm.ViewModels.Components;

/// <summary>
/// Strategy pattern for mapping image data scalars to colors
/// </summary>
internal interface IColorMappingStrategy 
{
    /// <summary>
    /// Apply this strategy. This is done by configuring <see cref="vtkImageMapToColors"/>
    /// </summary>
    void Apply(vtkImageMapToColors colorMap);
    
    void Update();
}