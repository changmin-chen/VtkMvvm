using Kitware.VTK;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels;

/// <summary>
/// Leverage VTK image actor instead of reslicing the image. Simpler and suitable for orthogonal slices.
/// </summary>
public class ImageOrthogonalSliceViewModel : VtkElementViewModel
{
    private readonly vtkImageMapToColors _colorMap;
    private int _sliceIndex;
    private double _windowLevel;
    private double _windowWidth;

    public ImageOrthogonalSliceViewModel(SliceOrientation orientation, ColoredImagePipeline pipeline)
    {
        Orientation = orientation;
        Actor = pipeline.Actor;
        ImageModel = ImageModel.Create(pipeline.Image);

        _colorMap = pipeline.ColorMap;
        pipeline.Connect();

        SetSliceIndex(0); // necessary. this not only affects which slice it initially displayed, but also the slicing orientation
    }

    public SliceOrientation Orientation { get; }
    public ImageModel ImageModel { get; }
    public override vtkImageActor Actor { get; }

    public int SliceIndex
    {
        get => _sliceIndex;
        set
        {
            if (SetField(ref _sliceIndex, value))
            {
                SetSliceIndex(value);
                OnModified();
            }
        }
    }

    public double WindowLevel
    {
        get => _windowLevel;
        set
        {
            if (SetField(ref _windowLevel, value))
            {
                SetWindowBand(value, WindowWidth);
                OnModified();
            }
        }
    }

    public double WindowWidth
    {
        get => _windowWidth;
        set
        {
            if (SetField(ref _windowWidth, value))
            {
                SetWindowBand(WindowLevel, value);
                OnModified();
            }
        }
    }

    public double Opacity
    {
        get => Actor.GetOpacity();
        set
        {
            if (Math.Abs(Actor.GetOpacity() - value) < 1e-3) return;
            Actor.SetOpacity(value);
            Actor.Modified();
            OnPropertyChanged();
            OnModified();
        }
    }

    private void SetSliceIndex(int sliceIndex)
    {
        int[] dims = ImageModel.Dimensions;
        switch (Orientation)
        {
            case SliceOrientation.Axial:
                Actor.SetDisplayExtent(0, dims[0] - 1, 0, dims[1] - 1, sliceIndex, sliceIndex);
                break;
            case SliceOrientation.Coronal:
                Actor.SetDisplayExtent(0, dims[0] - 1, sliceIndex, sliceIndex, 0, dims[2] - 1);
                break;
            case SliceOrientation.Sagittal:
                Actor.SetDisplayExtent(sliceIndex, sliceIndex, 0, dims[1] - 1, 0, dims[2] - 1);
                break;
        }

        Actor.Modified();
    }

    private void SetWindowBand(double level, double width)
    {
        double low = level - width * 0.5;
        double high = level + width * 0.5;

        vtkScalarsToColors? lut = _colorMap.GetLookupTable();
        lut.SetRange(low, high);
        lut.Build();
        _colorMap.SetLookupTable(lut);

        _colorMap.Modified();
        Actor.Modified();
    }
}