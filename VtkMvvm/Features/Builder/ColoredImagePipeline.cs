using Kitware.VTK;
using VtkMvvm.ViewModels;

namespace VtkMvvm.Features.Builder;

/// <summary>
/// DTO for the shared vtk image pipeline with color mapping.
/// The components may be shared across multiple <see cref="VtkElementViewModel"/>.
/// So the ViewModel should not dispose them. 
/// </summary>
public record ColoredImagePipeline(
    vtkImageData Image,
    vtkImageMapToColors ColorMap,
    bool IsLinearInterpolationOn
)
{
    /// <summary>
    ///     Connect pipeline: Image -> ColorMap -> Actor
    /// </summary>
    internal void Connect(vtkImageActor actor)
    {
        ColorMap.SetInput(Image);
        actor.SetInput(ColorMap.GetOutput());

        if (IsLinearInterpolationOn) actor.InterpolateOn();
        else actor.InterpolateOff();
    }
    
    /// <summary>
    /// Connect pipeline: Reslice → ColorMap → Actor
    /// </summary>
    internal void ConnectWithReslice(vtkImageActor actor, vtkImageReslice reslice)
    {
        reslice.SetInput(Image);
        ColorMap.SetInputConnection(reslice.GetOutputPort());
        actor.SetInput(ColorMap.GetOutput());
        
        if (IsLinearInterpolationOn) actor.InterpolateOn();
        else actor.InterpolateOff();
    }
}