using Kitware.VTK;

namespace VtkMvvm.Models;

// Simple model to hold image data and display properties
public record ImageModel(
    vtkImageData Value,
    int[] Dimensions,
    Extent Extent,
    double[] Center,
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
            image.GetCenter(),
            image.GetScalarRange()
        );
    }
}

public record Extent(int MinX, int MaxX, int MinY, int MaxY, int MinZ, int MaxZ);