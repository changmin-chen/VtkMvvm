using System.Numerics;
using Kitware.VTK;
using PresentationTest.TestData;
using ReactiveUI;
using VtkMvvm.Features.Builder;
using VtkMvvm.ViewModels;

namespace PresentationTest;

public class VtkObliqueSliceTestWindowViewModel : ReactiveObject
{
    private readonly vtkImageData _background;
    private readonly int[] _backgroundDims;

    private int _obliqueSliceIndex;
    public ImageObliqueSliceViewModel[] ImageVms { get; }

    public VtkObliqueSliceTestWindowViewModel()
    {
        _background = TestImageLoader.ReadNifti(@"TestData\CT_Abdo.nii.gz");
        _backgroundDims = _background.GetDimensions();

        ColoredImagePipelineBuilder bgBuilder = ColoredImagePipelineBuilder
            .WithImage(_background)
            .WithLinearInterpolation(true)
            .WithOpacity(1.0);

        ColoredImagePipeline pipe = bgBuilder.Build();

        Quaternion viewAngle = Quaternion.CreateFromYawPitchRoll(-20, -20, 45);
        ImageObliqueSliceViewModel axialVm = new(viewAngle, pipe);
        ImageVms = [axialVm];
    }

    public int ObliqueSliceIndex
    {
        get => _obliqueSliceIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _obliqueSliceIndex, value);
            SetSliceIndex(ImageVms, value);
        }
    }

    private static void SetSliceIndex(IList<ImageObliqueSliceViewModel> vms, int sliceIndex)
    {
        foreach (ImageObliqueSliceViewModel vm in vms) vm.SliceIndex = sliceIndex;
    }
}