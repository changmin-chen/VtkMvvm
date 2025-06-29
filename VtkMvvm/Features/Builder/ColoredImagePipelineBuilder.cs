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
    private bool _linearInterpolation = true;
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

    public ColoredImagePipelineBuilder WithLinearInterpolation(bool linear)
    {
        _linearInterpolation = linear;
        return this;
    }

    public ColoredImagePipelineBuilder WithRgbaLookupTable(vtkLookupTable lut)
    {
        _rgbaLookupTable = lut;
        return this;
    }

    public ColoredImagePipeline Build()
    {
        // 1) Create fresh colormap instances
        var colorMap = vtkImageMapToColors.New();

        // 2) Configure the lookup table
        if (_rgbaLookupTable is not null)
        {
            colorMap.SetLookupTable(_rgbaLookupTable);
            colorMap.SetOutputFormatToRGBA();
        }
        else
        {
            var grayLut = CreateDefaultGrayLut();
            colorMap.SetLookupTable(grayLut);
            colorMap.SetOutputFormatToLuminance();
        }

        return new ColoredImagePipeline(_image, colorMap, _linearInterpolation);
    }
}