using System.Collections.Immutable;
using System.Numerics;
using Kitware.VTK;
using PresentationTest.TestData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using VtkMvvm.Controls;
using VtkMvvm.Extensions;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;

namespace PresentationTest;

public class VtkObliqueSliceTestWindowViewModel : ReactiveObject
{
    private readonly vtkImageData _background;
    private readonly vtkCellPicker _picker = new();
    private readonly CrosshairViewModel _crosshair;

    private int _obliqueSliceIndex;
    public ImageObliqueSliceViewModel[] ObliqueImageVms { get; private set; }
    public ImmutableList<VtkElementViewModel> ObliqueOverlayVms => [_crosshair];
    [Reactive] public float YawDegrees { get; set; } = -20;
    [Reactive] public float PitchDegrees { get; set; } = -20;
    [Reactive] public float RollDegrees { get; set; } = 45;

    public VtkObliqueSliceTestWindowViewModel()
    {
        _background = TestImageLoader.ReadNifti(@"TestData\CT_Abdo.nii.gz");

        ColoredImagePipelineBuilder bgBuilder = ColoredImagePipelineBuilder
            .WithImage(_background)
            .WithLinearInterpolation(true)
            .WithOpacity(1.0);

        ColoredImagePipeline pipe = bgBuilder.Build();

        Quaternion slicingAngle = Quaternion.CreateFromYawPitchRoll(
            DegreesToRadius(YawDegrees),
            DegreesToRadius(PitchDegrees),
            DegreesToRadius(RollDegrees));
        ImageObliqueSliceViewModel axialVm = new(slicingAngle, pipe);

        ObliqueImageVms = [axialVm];
        UpdateSlicingAngleCommand = new DelegateCommand(UpdateSlicingAngle);

        // Crosshair
        _crosshair = new CrosshairViewModel(SliceOrientation.Axial, _background.GetBounds());
    }

    public void OnControlGetMouseDisplayPosition(VtkObliqueImageSceneControl sender, int x, int y)
    {
        if (_picker.Pick(x, y, 0, sender.MainRenderer) == 0) return;

        Double3 clickWorldPos = _picker.GetPickWorldPosition();
        _crosshair.FocalPoint = clickWorldPos;
    }

    public DelegateCommand UpdateSlicingAngleCommand { get; }

    public int ObliqueSliceIndex
    {
        get => _obliqueSliceIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _obliqueSliceIndex, value);
            SetSliceIndex(ObliqueImageVms, value);
        }
    }

    private void UpdateSlicingAngle()
    {
        var slicingAngle = Quaternion.CreateFromYawPitchRoll(
            DegreesToRadius(YawDegrees),
            DegreesToRadius(PitchDegrees),
            DegreesToRadius(RollDegrees));
        
        ObliqueImageVms[0].SliceOrientation = slicingAngle;
    }

    private static void SetSliceIndex(IList<ImageObliqueSliceViewModel> vms, int sliceIndex)
    {
        foreach (ImageObliqueSliceViewModel vm in vms) vm.SliceIndex = sliceIndex;
    }

    private static float DegreesToRadius(float degrees) => (float)(degrees * Math.PI / 180);
}