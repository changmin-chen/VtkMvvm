using Kitware.VTK;

namespace PresentationTest.Extensions;

public static class VtkImageDataExtension
{
    /// <summary>
    ///     Make the scalars of this image with zero value
    /// </summary>
    public static void ZeroScalars(this vtkImageData image)
    {
        int numComponents = image.GetNumberOfScalarComponents();
        long numPoints = image.GetNumberOfPoints();
        int numBytesPerComponent = image.GetScalarSize();

        IntPtr scalarPtr = image.GetScalarPointer();
        unsafe
        {
            byte* ptr = (byte*)scalarPtr.ToPointer();
            for (int i = 0; i < numComponents * numPoints * numBytesPerComponent; i++) ptr[i] = 0;
        }
    }
}