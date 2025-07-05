using Kitware.VTK;

namespace VtkMvvm.Features.Builder;

/// <summary>
/// Builder for Image -> MapToColor pipeline.
/// When Build(), the Image will not be copied. 
/// </summary>
public class ColoredImagePipelineBuilder
{
    private readonly vtkImageData _image;

    // Configuration fields only:
    private vtkLookupTable? _rgbaLookupTable; // null means Luminance

    private ColoredImagePipelineBuilder(vtkImageData image) => _image = image;

    public static ColoredImagePipelineBuilder WithSharedImage(vtkImageData image) => new(image);

    private vtkLookupTable CreateDefaultGrayLut()
    {
        vtkLookupTable? grayLut = vtkLookupTable.New();
        double[]? range = _image.GetScalarRange();
        grayLut.SetRange(range[0], range[1]);
        grayLut.SetValueRange(0.0, 1.0);
        grayLut.SetSaturationRange(0.0, 0.0);
        grayLut.Build();
        return grayLut;
    }

    public ColoredImagePipelineBuilder WithRgbaLookupTable(vtkLookupTable lut)
    {
        _rgbaLookupTable = lut;
        return this;
    }

    public ColoredImagePipeline Build()
    {
        bool isRgba = _rgbaLookupTable != null;
        vtkLookupTable lut = _rgbaLookupTable ??= CreateDefaultGrayLut();

        return new ColoredImagePipeline
        {
            Image = _image,
            LookupTable = lut,
            IsRgba = isRgba
        };
    }
}