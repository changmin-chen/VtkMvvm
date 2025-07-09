using Kitware.VTK;

namespace VtkMvvm.ViewModels.Components;

/// <summary>
/// Strategy pattern for mapping image data scalars to colors
/// </summary>
internal interface IColorMappingStrategy 
{
    /// <summary>
    /// Apply this strategy onto the colormap. This may mutate the <see cref="vtkImageMapToColors"/>.
    /// </summary>
    void ApplyTo(vtkImageMapToColors colorMap);
    
    void Update();
}