using Kitware.VTK;

namespace UnitTest;

internal static class VtkTestUtils
{
    public static vtkImageData Capture(vtkRenderWindow rw, int bufferType = 4 /*RGB*/)
    {
        using var w2i = vtkWindowToImageFilter.New();
        w2i.SetInput(rw);
        w2i.SetInputBufferType(bufferType);
        w2i.Update();
        return w2i.GetOutput();
    }

    public static double Compare(vtkImageData img1, vtkImageData img2, double threshold = 1e-3)
    {
        using var diff = vtkImageDifference.New();
        diff.SetInput(img1);
        diff.SetImage(img2);
        diff.Update();
        return diff.GetThresholdedError();
    }

    public static vtkImageData LoadPng(string path)
    {
        using var reader = vtkPNGReader.New();
        reader.SetFileName(path);
        reader.Update();
        return reader.GetOutput();
    }
}
