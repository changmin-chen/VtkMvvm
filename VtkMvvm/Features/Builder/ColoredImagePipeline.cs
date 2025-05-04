using Kitware.VTK;

namespace VtkMvvm.Features.Builder;

/// <summary>
///     DTO for color-mapped image pipeline
/// </summary>
public record ColoredImagePipeline(
    vtkImageData Image,
    vtkImageMapToColors ColorMap,
    vtkImageActor Actor
)
{
    public void Connect()
    {
        ColorMap.SetInput(Image);
        Actor.SetInput(ColorMap.GetOutput());
        Actor.Modified();
    }
}