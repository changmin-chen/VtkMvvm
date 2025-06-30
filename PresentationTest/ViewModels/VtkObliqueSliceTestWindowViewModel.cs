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

    // Underlay: background image
    private readonly ImageObliqueSliceViewModel _obliqueImageVm;

    // Overlay: crosshair
    private readonly CrosshairViewModel _crosshair;
    public CrosshairViewModel CrosshairVm => _crosshair;

    // Collection of VtkElementViewModel binds to VTK scene control
    public ImmutableList<ImageObliqueSliceViewModel> ObliqueImageVms => [_obliqueImageVm];
    public ImmutableList<VtkElementViewModel> ObliqueOverlayVms => [_crosshair];
    [Reactive] public float YawDegrees { get; set; } = 0;
    [Reactive] public float PitchDegrees { get; set; } = 0;
    [Reactive] public float RollDegrees { get; set; } = 45;

    public VtkObliqueSliceTestWindowViewModel()
    {
        UpdateSliceOrientationCommand = new DelegateCommand(UpdateSliceOrientation);

        _background = TestImageLoader.ReadNifti(@"TestData\CT_Abdo.nii.gz");

        var bgPipe = ColoredImagePipelineBuilder
            .WithSharedImage(_background)
            .Build();

        var sliceOrientation = Quaternion.CreateFromYawPitchRoll(
            DegreesToRadius(YawDegrees),
            DegreesToRadius(PitchDegrees),
            DegreesToRadius(RollDegrees));
        var obliqueVm = new ImageObliqueSliceViewModel(sliceOrientation, bgPipe);


        // Pick list
        _picker.SetTolerance(0.005);
        _picker.PickFromListOn();
        _picker.AddPickList(obliqueVm.Actor);

        // Initialize ViewModels
        _obliqueImageVm = obliqueVm;
        _crosshair = CrosshairViewModel.Create(
            obliqueVm.PlaneAxisU,
            obliqueVm.PlaneAxisV,
            obliqueVm.GetSliceBounds());
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

        _obliqueImageVm.SliceOrientation = sliceOrientation;

        // let crosshair always being plotted onto the resliced plane
        Bounds bounds = _obliqueImageVm.GetSliceBounds();
        _crosshair.SetBounds(bounds);
        Debug.WriteLine($"Slice bounds: {bounds}");
        _crosshair.SetPlaneAxes(_obliqueImageVm.PlaneAxisU, _obliqueImageVm.PlaneAxisV);
    }

    private static void SetSliceIndex(IList<ImageObliqueSliceViewModel> vms, int sliceIndex)
    {
        foreach (ImageObliqueSliceViewModel vm in vms) vm.SliceIndex = sliceIndex;
    }

    private static float DegreesToRadius(float degrees) => (float)(degrees * Math.PI / 180);
}