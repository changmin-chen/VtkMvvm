using Kitware.VTK;
using VtkMvvm.Features.Builder;

namespace VtkMvvm.ViewModels.Components;

internal sealed class LabelMapColorMapping : IColorMappingStrategy
{
    private readonly vtkLookupTable _lut;  // injected

    public LabelMapColorMapping(ColoredImagePipeline pipe)
    {
        _lut = pipe.LookupTable;
    }

    public void Apply(vtkImageMapToColors colorMap)
    {
        colorMap.SetLookupTable(_lut);
        colorMap.SetOutputFormatToRGBA();
        colorMap.PassAlphaToOutputOn();
        Update();
    }

    public void Update() { /* nothing dynamic for now */ }

}