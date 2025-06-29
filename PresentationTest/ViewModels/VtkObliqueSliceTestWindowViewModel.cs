using System.Collections.Immutable;
using System.Diagnostics;
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
    public ImageObliqueSliceViewModel ObliqueImageVm { get; private set; }
    public CrosshairViewModel CrosshairVm { get; }
    public ImmutableList<ImageObliqueSliceViewModel> ObliqueImageVms => [ObliqueImageVm];
    public ImmutableList<VtkElementViewModel> ObliqueOverlayVms => [CrosshairVm];
    [Reactive] public float YawDegrees { get; set; } = -20;
    [Reactive] public float PitchDegrees { get; set; } = -20;
    [Reactive] public float RollDegrees { get; set; } = 45;

    public VtkObliqueSliceTestWindowViewModel()
    {
        UpdateSliceOrientationCommand = new DelegateCommand(UpdateSliceOrientation);
        
        _background = TestImageLoader.ReadNifti(@"TestData\CT_Abdo.nii.gz");

        var bgBuilder = ColoredImagePipelineBuilder
            .WithSharedImage(_background)
            .WithLinearInterpolation(true);
        
        ColoredImagePipeline pipe = bgBuilder.Build();

        var sliceOrientation = Quaternion.CreateFromYawPitchRoll(
            DegreesToRadius(YawDegrees),
            DegreesToRadius(PitchDegrees),
            DegreesToRadius(RollDegrees));
        var obliqueVm = new ImageObliqueSliceViewModel(sliceOrientation, pipe);
        
        // Pick list
        _picker.SetTolerance(0.005);
        _picker.PickFromListOn();
        _picker.AddPickList(obliqueVm.Actor);
        
        // Initialize ViewModels
        ObliqueImageVm = obliqueVm;
        var lineBounds = obliqueVm.GetSliceBounds();
        CrosshairVm = new CrosshairViewModel(Double3.UnitX, Double3.UnitY, lineBounds);
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
        var sliceOrientation = Quaternion.CreateFromYawPitchRoll(
            DegreesToRadius(YawDegrees),
            DegreesToRadius(PitchDegrees),
            DegreesToRadius(RollDegrees));

        ObliqueImageVm.SliceOrientation = sliceOrientation;

        Bounds b = ObliqueImageVm.GetSliceBounds();
        CrosshairVm.UpdateBounds(b);
        Debug.WriteLine($"Slice bounds: {b}");
    }

    private static void SetSliceIndex(IList<ImageObliqueSliceViewModel> vms, int sliceIndex)
    {
        foreach (ImageObliqueSliceViewModel vm in vms) vm.SliceIndex = sliceIndex;
    }

    private static float DegreesToRadius(float degrees) => (float)(degrees * Math.PI / 180);
}