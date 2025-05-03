using Kitware.VTK;

namespace VtkMvvm.Features.Builder;

/// <summary>
/// Builder for Image -> MapToColor -> ImageActor pipeline.
/// </summary>
public class ColoredImagePipelineBuilder
{
    private readonly vtkImageData _image;

    // Configuration fields only:
    private bool _isPickable = true;
    private bool _linearInterpolation = true;
    private double _opacity = 1.0;
    private vtkLookupTable? _rgbaLookupTable; // null means Luminance

    public ColoredImagePipelineBuilder(vtkImageData image)
    {
        _image = image;
    }

    private vtkLookupTable CreateDefaultGrayLut()
    {
        vtkLookupTable? grayLut = vtkLookupTable.New();
        double[]? range = _image.GetScalarRange();
        grayLut.SetRange(range[0], range[1]);
        grayLut.SetValueRange(0.2, 1.0);
        grayLut.SetSaturationRange(0.0, 0.0);
        grayLut.Build();
        return grayLut;
    }

    public static ColoredImagePipelineBuilder WithImage(vtkImageData image)
        => new(image);

    public ColoredImagePipelineBuilder WithPickable(bool pickable)
    {
        _isPickable = pickable;
        return this;
    }

    public ColoredImagePipelineBuilder WithOpacity(double opacity)
    {
        if (opacity < 0 || opacity > 1)
            throw new ArgumentOutOfRangeException(nameof(opacity));
        _opacity = opacity;
        return this;
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
        // 1) Create fresh filter & actor instances
        var colorMap = vtkImageMapToColors.New();
        var actor = vtkImageActor.New();

        // 2) Configure the lookup table
        if (_rgbaLookupTable is not null)
        {
            colorMap.SetLookupTable(_rgbaLookupTable);
            colorMap.SetOutputFormatToRGBA();
            actor.SetOpacity(1.0); // RGBA must carry its own alpha
        }
        else
        {
            var grayLut = CreateDefaultGrayLut();
            colorMap.SetLookupTable(grayLut);
            colorMap.SetOutputFormatToLuminance();
            actor.SetOpacity(_opacity);
        }

        // 3) Wire up image → colorMap → actor
        colorMap.SetInput(_image);
        actor.SetInput(colorMap.GetOutput());

        // 4) Other actor settings
        if (_isPickable) actor.PickableOn();
        else actor.PickableOff();

        if (_linearInterpolation) actor.InterpolateOn();
        else actor.InterpolateOff();

        return new ColoredImagePipeline(_image, colorMap, actor);
    }
}