using Kitware.VTK;
using VtkMvvm.ViewModels.Base;

namespace VtkMvvm.Features.Builder;


/// <summary>
/// Data model for the shared vtk image pipeline with color mapping.
/// The image data may be shared across multiple <see cref="VtkElementViewModel"/>.
/// </summary>
public sealed class ColoredImagePipeline : IDisposable
{
    public required vtkImageData Image { get; init; }
    public required vtkLookupTable LookupTable { get; init; }
    public required bool IsRgba { get; init; }

    public void Dispose()
    {
        Image.Dispose();
        LookupTable.Dispose();
    }
}

// internal static class ColoredImagePipelineExtensions
// {
//     /// <summary>
//     ///     Connect pipeline: Image -> ColorMap -> Actor
//     /// </summary>
//     public static void Connect(this ColoredImagePipeline pipe, vtkImageMapToColors colorMap, vtkImageActor actor)
//     {
//         colorMap.ConfigureColorMap(pipe);
//
//         colorMap.SetInput(pipe.Image);
//         actor.SetInput(colorMap.GetOutput());
//
//         if (pipe.IsLinearInterpolationOn) actor.InterpolateOn();
//         else actor.InterpolateOff();
//     }
//     
//     /// <summary>
//     /// Connect pipeline: Image -> Reslice Image → ColorMap → Actor
//     /// </summary>
//     public static void ConnectWithReslice(this ColoredImagePipeline pipe, vtkImageMapToColors colorMap, vtkImageReslice reslice, vtkImageActor actor)
//     {
//         colorMap.ConfigureColorMap(pipe);
//
//         reslice.SetInput(pipe.Image);
//         colorMap.SetInputConnection(reslice.GetOutputPort());
//         actor.SetInput(colorMap.GetOutput());
//
//         if (pipe.IsLinearInterpolationOn) actor.InterpolateOn();
//         else actor.InterpolateOff();
//     }
//
//     // ---- Helpers ------------------------------------
//     public static void ConfigureColorMap(this vtkImageMapToColors colorMap, ColoredImagePipeline pipe)
//     {
//         colorMap.SetLookupTable(pipe.LookupTable);
//
//         if (pipe.IsRgba) colorMap.SetOutputFormatToRGBA();
//         else colorMap.SetOutputFormatToLuminance();
//     }
// }