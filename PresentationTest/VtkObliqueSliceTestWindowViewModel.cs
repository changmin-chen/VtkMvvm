using System.Numerics;
using Kitware.VTK;
using PresentationTest.TestData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
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

        Quaternion slicingAngle = Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, Roll);
        ImageObliqueSliceViewModel axialVm = new(slicingAngle, pipe);
        ImageVms = [axialVm];
        
        UpdateSlicingAngleCommand = new DelegateCommand(UpdateSlicingAngle);
    }

    [Reactive] public float Yaw { get; set; } = -20;
    [Reactive] public float Pitch { get; set; } = -20;
    [Reactive] public float Roll { get; set; } = 45;
    
    public DelegateCommand UpdateSlicingAngleCommand { get; }

    public int ObliqueSliceIndex
    {
        get => _obliqueSliceIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _obliqueSliceIndex, value);
            SetSliceIndex(ImageVms, value);
        }
    }

    private void UpdateSlicingAngle()
    {
        var slicingAngle = Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, Roll);
        ImageVms[0].SliceOrientation = slicingAngle;
    }
    private static void SetSliceIndex(IList<ImageObliqueSliceViewModel> vms, int sliceIndex)
    {
        foreach (ImageObliqueSliceViewModel vm in vms) vm.SliceIndex = sliceIndex;
    }
}