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
using VtkMvvm.ViewModels.Base;

namespace PresentationTest.ViewModels;

public class VtkObliqueSliceTestWindowViewModel : ReactiveObject
{
    private readonly vtkImageData _background;
    private readonly vtkCellPicker _picker = new();

    private int _obliqueSliceIndex;

    // Underlay: background image -----------------------------
    private readonly ImageObliqueSliceViewModel _obliqueVm;

    // Overlay: ----------------------------------------
    private readonly CrosshairViewModel _crosshair;
    private readonly BullseyeViewModel _bullseye;
    public CrosshairViewModel CrosshairVm => _crosshair;

    // Collection of VtkElementViewModel binds to VTK scene control
    public ImmutableList<ImageObliqueSliceViewModel> ObliqueImageVms => [_obliqueVm];
    public ImmutableList<VtkElementViewModel> ObliqueOverlayVms => [_crosshair, _bullseye];
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

        // Underlay viewModels ---------------------------------------------------
        _obliqueVm = obliqueVm;

        // Overlay viewModels ----------------------------------------------------
        _crosshair = CrosshairViewModel.Create(
            obliqueVm.PlaneAxisU,
            obliqueVm.PlaneAxisV,
            Bounds.FromArray(_background.GetBounds()));  // get volume bounds

        _bullseye = BullseyeViewModel.Create(Double3.Zero, obliqueVm.PlaneNormal);
    }

    public void OnControlGetMouseDisplayPosition(VtkObliqueImageSceneControl sender, int x, int y)
    {
        if (_picker.Pick(x, y, 0, sender.MainRenderer) == 0) return;

        Double3 clickWorldPos = _picker.GetPickWorldPosition();
        _crosshair.FocalPoint = clickWorldPos;
        _bullseye.FocalPoint = clickWorldPos;
        InvalidateBullseyeVisibility();
    }

    public DelegateCommand UpdateSliceOrientationCommand { get; }

    public int ObliqueSliceIndex
    {
        get => _obliqueSliceIndex;
        set
        {
            value = Math.Clamp(value, _obliqueVm.MinSliceIndex, _obliqueVm.MaxSliceIndex);
            this.RaiseAndSetIfChanged(ref _obliqueSliceIndex, value);
            SetSliceIndex(ObliqueImageVms, value);
            InvalidateBullseyeVisibility();
        }
    }

    /// <summary>
    /// Simulate we want to "Fix" the bullseye focal point.
    /// And let it invisible if the user change the displayed slice that is far from focal point. 
    /// </summary>
    private void InvalidateBullseyeVisibility()
    {
        Double3 p = _bullseye.FocalPoint;
        // n = slice normal (already exposed), p = focal-point of the bullseye
        double signedDistance =
            Vector3.Dot(_obliqueVm.PlaneNormal, (Vector3)(p - _obliqueVm.PlaneOrigin));

        bool onCurrentSlice = Math.Abs(signedDistance) < 4.5; // 4.5-mm, about 1~3 slices 
        _bullseye.Visible = onCurrentSlice;
        if (!onCurrentSlice) Debug.WriteLine($"Bullseye visibility set to false. Signed distance: '{signedDistance:F2}' exceeds limits. ");
    }
    
    private void UpdateSliceOrientation()
    {
        var sliceOrientation = Quaternion.CreateFromYawPitchRoll(
            DegreesToRadius(YawDegrees),
            DegreesToRadius(PitchDegrees),
            DegreesToRadius(RollDegrees));

        _obliqueVm.SliceOrientation = sliceOrientation;
        var (uDir, vDir, nDir) = (_obliqueVm.PlaneAxisU, _obliqueVm.PlaneAxisV, _obliqueVm.PlaneNormal);

        // Adjust overlays, so they can plot onto the resliced plane
        _crosshair.SetPlaneAxes(uDir, vDir);
        _bullseye.Normal = nDir;
    }
    
    

    private static void SetSliceIndex(IList<ImageObliqueSliceViewModel> vms, int sliceIndex)
    {
        foreach (ImageObliqueSliceViewModel vm in vms) vm.SliceIndex = sliceIndex;
    }

    private static float DegreesToRadius(float degrees) => (float)(degrees * Math.PI / 180);
}