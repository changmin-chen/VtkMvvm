using Kitware.VTK;
using VtkMvvm.Features.Builder;
using VtkMvvm.ViewModels.Base;

namespace VtkMvvm.ViewModels.Components;

public sealed class WindowLevelColorMapping : ViewModelBase, IColorMappingStrategy
{
    private readonly vtkLookupTable _lut;
    private vtkImageMapToColors? _cmap;

    public WindowLevelColorMapping(ColoredImagePipeline pipe)
    {
        _lut = pipe.LookupTable;
    }

    private double _window = 400;
    private double _level = 40;

    public double Window
    {
        get => _window;
        set
        {
            if (Math.Abs(_window - value) < 1e-3) return;
            _window = value;
            Update();
            OnPropertyChanged();
        }
    }

    public double Level
    {
        get => _level;
        set
        {
            if (Math.Abs(_level - value) < 1e-3) return;
            _level = value;
            Update();
            OnPropertyChanged();
        }
    }

    public void Apply(vtkImageMapToColors cmap)
    {
        _cmap = cmap;
        _cmap.SetLookupTable(_lut);
        _cmap.PassAlphaToOutputOff();
        Update();
    }

    public void Update()
    {
        if (_cmap == null) return;

        double low = Level - Window / 2.0;
        double high = Level + Window / 2.0;

        _lut.SetRange(low, high); // maps scalar → [0,1]
        _lut.SetValueRange(0.0, 1.0); // brightness
        _lut.SetSaturationRange(0.0, 0.0); // greyscale
        _lut.Build();

        _cmap.Modified();
    }

    public void Dispose() => _lut.Dispose();
    
}