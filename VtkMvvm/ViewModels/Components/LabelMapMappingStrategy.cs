using Kitware.VTK;
using VtkMvvm.Features.Builder;

namespace VtkMvvm.ViewModels.Components;

internal sealed class LabelMapColorMapping : IColorMappingStrategy
{
    private readonly vtkLookupTable _lut;
    private vtkImageMapToColors? _cmap;

    public LabelMapColorMapping(ColoredImagePipeline pipe)
    {
        _lut = pipe.LookupTable;
    }

    public void Apply(vtkImageMapToColors cmap)
    {
        _cmap = cmap;
        _cmap.SetLookupTable(_lut);
        _cmap.SetOutputFormatToRGBA();
        _cmap.PassAlphaToOutputOn();
        Update();
    }

    public void Update() { /* nothing dynamic for now */ }

    public void Dispose() => _lut.Dispose();
}