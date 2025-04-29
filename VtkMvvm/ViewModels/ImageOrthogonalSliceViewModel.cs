using System.Diagnostics;
using Kitware.VTK;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels;

/// <summary>
/// Leverage VTK image actor instead of reslicing the image. Simpler and suitable for orthogonal slices.
/// </summary>
public class ImageOrthogonalSliceViewModel : VtkElementViewModel
{
    private int _sliceIndex;
    public SliceOrientation Orientation { get; }

    public override vtkImageActor Actor { get; }

    public ImageOrthogonalSliceViewModel(SliceOrientation orientation, ColoredImagePipeline p) : base(p.Image)
    {
        Orientation = orientation;
        Actor = p.Actor;

        p.Connect();
        SetSliceIndex(0);
    }

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

    private void SetSliceIndex(int sliceIndex)
    {
        Debug.WriteLine($"{Orientation}-SliceIndex: {sliceIndex}");

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
}