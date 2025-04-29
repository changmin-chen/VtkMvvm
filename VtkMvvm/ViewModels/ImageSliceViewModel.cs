using Kitware.VTK;
using VtkMvvm.Extensions;
using VtkMvvm.Obsolete;

namespace VtkMvvm.ViewModels;

public class ImageSliceViewModel : VtkElementViewModel
{
    private readonly vtkImageActor _actor;
    private readonly vtkImageShiftScale _shiftScale;
    private readonly VtkImageSlicer _slicer;
    private double _higherDisplayRange;
    private double _lowerDisplayRange;

    private int _sliceIndex;

    private ImageSliceViewModel(vtkImageData image, VtkImageSlicer slicer) : base(image)
    {
        _actor = new vtkImageActor();
        _slicer = slicer;

        // Set up display preparation
        _shiftScale = new vtkImageShiftScale();
        _shiftScale.SetOutputScalarTypeToUnsignedChar();
        _shiftScale.ClampOverflowOn();
        _shiftScale.SetInputConnection(_slicer.GetOutputPort());
        _actor.SetInput(_shiftScale.GetOutput());

        _slicer.SetSliceIndex(0);
        var (lower, higher) = image.GetPercentileRange();
        _lowerDisplayRange = lower;
        _higherDisplayRange = higher;
        UpdateDisplay(_lowerDisplayRange, _higherDisplayRange);

        Actor = _actor;
    }

    public override vtkProp Actor { get; }

    public int SliceIndex
    {
        get => _sliceIndex;
        set
        {
            if (SetField(ref _sliceIndex, value)) SetSliceIndex(value);
        }
    }

    public double LowerDisplayRange
    {
        get => _lowerDisplayRange;
        set
        {
            if (SetField(ref _lowerDisplayRange, value)) UpdateDisplay(_lowerDisplayRange, _higherDisplayRange);
        }
    }

    public double HigherDisplayRange
    {
        get => _higherDisplayRange;
        set
        {
            if (SetField(ref _higherDisplayRange, value)) UpdateDisplay(_lowerDisplayRange, _higherDisplayRange);
        }
    }

    public static ImageSliceViewModel CreateAxial(vtkImageData image)
        => new(image, new VtkAxialSlicer(image));

    public static ImageSliceViewModel CreateCoronal(vtkImageData image)
        => new(image, new VtkCoronalSlicer(image));

    public static ImageSliceViewModel CreateSagittal(vtkImageData image)
        => new(image, new VtkSagittalSlicer(image));

    /// <summary>
    /// Set the slice index of the image slice. The slicing direction is determined by the slicer type.
    /// </summary>
    /// <param name="sliceIndex"></param>
    private void SetSliceIndex(int sliceIndex)
    {
        _slicer.SetSliceIndex(sliceIndex);

        _actor.Modified();
        OnModified();
    }

    /// <summary>
    /// Update the display range of the image slice.
    /// </summary>
    /// <param name="lower">The lower pixel value of the image.</param>
    /// <param name="higher">The higher pixel value of the image.</param>
    private void UpdateDisplay(double lower, double higher)
    {
        // If lower is greater than higher, clamp lower to higher
        if (higher <= lower) lower = higher;
        var shift = -lower;
        var scale = 255.0 / (higher - lower);
        _shiftScale.SetShift(shift);
        _shiftScale.SetScale(scale);
        _shiftScale.Update();

        _actor.Modified();
        OnModified();
    }
}