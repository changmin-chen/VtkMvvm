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

namespace PresentationTest.ViewModels;

public class VtkObliqueSliceTestWindowViewModel : ReactiveObject
{
    private readonly vtkImageData _background;
    private readonly vtkCellPicker _picker = new();

    private int _obliqueSliceIndex;
    public ImageObliqueSliceViewModel[] ObliqueImageVms { get; private set; }
    public CrosshairBoxViewModel CrosshairVm { get; }
    public ImmutableList<VtkElementViewModel> ObliqueOverlayVms => [CrosshairVm];
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
        ImageObliqueSliceViewModel obliqueVm = new(slicingAngle, pipe);

        ObliqueImageVms = [obliqueVm];
        UpdateSliceOrientationCommand = new DelegateCommand(UpdateSliceOrientation);

        // Crosshair
        // CrosshairVm = new CrosshairViewModel(SliceOrientation.Axial, _background.GetBounds());
        CrosshairVm = new CrosshairBoxViewModel( obliqueVm.PlaneAxisU, obliqueVm.PlaneAxisV, _background.GetBounds());
        
        // Pick list
        _picker.SetTolerance(0.005);
        _picker.PickFromListOn();
        _picker.AddPickList(obliqueVm.Actor);
    }

    public void OnControlGetMouseDisplayPosition(VtkObliqueImageSceneControl sender, int x, int y)
    {
        if (_picker.Pick(x, y, 0, sender.MainRenderer) == 0) return;

        Double3 clickWorldPos = _picker.GetPickWorldPosition();
        CrosshairVm.FocalPoint = clickWorldPos;
    }

    public DelegateCommand UpdateSliceOrientationCommand { get; }

    public int ObliqueSliceIndex
    {
        get => _obliqueSliceIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _obliqueSliceIndex, value);
            SetSliceIndex(ObliqueImageVms, value);
        }
    }

    private void UpdateSliceOrientation()
    {
        var slicingAngle = Quaternion.CreateFromYawPitchRoll(
            DegreesToRadius(YawDegrees),
            DegreesToRadius(PitchDegrees),
            DegreesToRadius(RollDegrees));
        
        ObliqueImageVms[0].SliceOrientation = slicingAngle;
        CrosshairVm.UpdatePlaneAxes(ObliqueImageVms[0].PlaneAxisU, ObliqueImageVms[0].PlaneAxisV);
    }

    private static void SetSliceIndex(IList<ImageObliqueSliceViewModel> vms, int sliceIndex)
    {
        foreach (ImageObliqueSliceViewModel vm in vms) vm.SliceIndex = sliceIndex;
    }

    private static float DegreesToRadius(float degrees) => (float)(degrees * Math.PI / 180);
}