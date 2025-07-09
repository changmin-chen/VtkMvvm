using Kitware.VTK;

namespace VtkMvvm.Models;

/// <summary>
/// Simple model to hold image data and display properties
/// </summary>
public record ImageModel(
    vtkImageData Image,
    int[] Dims,
    Extent Extent,
    double[] Origin,
    double[] Center,
    double[] Spacing,
    double[] ScalarRange
)
{
    public static ImageModel Create(vtkImageData image)
    {
        var ext = image.GetExtent();

        return new(
            image,
            image.GetDimensions(),
            new Extent(ext[0], ext[1], ext[2], ext[3], ext[4], ext[5]),
            image.GetOrigin(),
            image.GetCenter(),
            image.GetSpacing(),
            image.GetScalarRange()
        );
    }
}

public readonly record struct Extent(int MinX, int MaxX, int MinY, int MaxY, int MinZ, int MaxZ);