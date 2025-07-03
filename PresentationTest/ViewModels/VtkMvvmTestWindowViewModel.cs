using System.Numerics;
using Kitware.VTK;
using PresentationTest.Constants;
using PresentationTest.Extensions;
using PresentationTest.TestData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using VtkMvvm.Controls;
using VtkMvvm.Extensions;
using VtkMvvm.Features.BrushPainter;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;
using VtkMvvm.ViewModels.Base;

namespace PresentationTest.ViewModels;

public class VtkMvvmTestWindowViewModel : ReactiveObject
{
    // Go with image data
    private readonly vtkImageData _background;
    private readonly int[] _backgroundDims;

    // Underlay: image and labelmap
    private readonly ImageOrthogonalSliceViewModel _axialVm, _coronalVm, _sagittalVm, _axialLabelVm, _coronalLabelVm, _sagittalLabelVm;
    private readonly ImageObliqueSliceViewModel _obliqueVm, _obliqueLabelVm;

    // Overlay: crosshairs, slice-labels, brush
    private readonly CrosshairViewModel _axialCrosshairVm, _coronalCrosshairVm, _sagittalCrosshairVm;
    private readonly BullseyeViewModel _obliqueBullseyeVm;

    // Brush
    private readonly vtkImageData _labelMap;
    private readonly vtkLookupTable _labelMapLut = LabelMapLookupTable.NewTable();
    private readonly BrushLinearOffsetCache _offsetsConverter = new();
    private readonly VoxelPainter _painter = new();

    // Painting labelmap
    private readonly vtkCellPicker _picker = new();
    private int _axialSliceIndex;
    private int _coronalSliceIndex;
    private int _sagittalSliceIndex;
    private int _obliqueSliceIndex;

    // Background ViewModels: Axial, Coronal, Sagittal slice view models
    public ImageOrthogonalSliceViewModel[] AxialVms => [_axialVm, _axialLabelVm];
    public ImageOrthogonalSliceViewModel[] CoronalVms => [_coronalVm, _coronalLabelVm];
    public ImageOrthogonalSliceViewModel[] SagittalVms => [_sagittalVm, _sagittalLabelVm];
    public ImageObliqueSliceViewModel[] ObliqueVms => [_obliqueVm, _obliqueLabelVm];

    // Overlay ViewModels
    private readonly BrushViewModel _brushVm = new() { Diameter = 5.0 };
    public BrushViewModel BrushVm => _brushVm; // directly binds to slider for setting diameter
    public VtkElementViewModel[] AxialOverlayVms => [_brushVm, _axialCrosshairVm];
    public VtkElementViewModel[] CoronalOverlayVms => [_brushVm, _coronalCrosshairVm];
    public VtkElementViewModel[] SagittalOverlayVms => [_brushVm, _sagittalCrosshairVm];
    public VtkElementViewModel[] ObliqueOverlayVms => [_obliqueBullseyeVm];

    // -- Oblique slice orientation -------------------------
    [Reactive] public float YawDegrees { get; set; } = 33;
    [Reactive] public float PitchDegrees { get; set; } = -45;
    [Reactive] public float RollDegrees { get; set; }


    public VtkMvvmTestWindowViewModel()
    {
        _background = TestImageLoader.ReadNifti(@"TestData\CT_Abdo.nii.gz");
        _backgroundDims = _background.GetDimensions();

        var sliceOrientation = Quaternion.CreateFromYawPitchRoll(Deg2Rad(YawDegrees),
            Deg2Rad(PitchDegrees),
            Deg2Rad(RollDegrees));
        sliceOrientation = Quaternion.Normalize(sliceOrientation);

        // Build the shared background image pipeline
        var bgPipe = ColoredImagePipelineBuilder
            .WithSharedImage(_background)
            .WithLinearInterpolation(true)
            .Build();

        // Build the shared labelmap image pipeline
        _labelMap = CreateLabelMap(_background);
        var labelMapPipe = ColoredImagePipelineBuilder
            .WithSharedImage(_labelMap)
            .WithLinearInterpolation(false)
            .WithRgbaLookupTable(_labelMapLut)
            .Build();

        _axialVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Axial, bgPipe);
        _axialLabelVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Axial, labelMapPipe);

        _coronalVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Coronal, bgPipe);
        _coronalLabelVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Coronal, labelMapPipe);

        _sagittalVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Sagittal, bgPipe);
        _sagittalLabelVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Sagittal, labelMapPipe);

        _obliqueVm = new ImageObliqueSliceViewModel(sliceOrientation, bgPipe);
        _obliqueLabelVm = new ImageObliqueSliceViewModel(sliceOrientation, labelMapPipe);

        // Add brushes that render on top of the image
        _brushVm.Diameter = 5.0;

        // Instantiate voxel-brush and cached
        double[]? spacing = _labelMap.GetSpacing();
        _brushVm.Height = spacing.Max();
        _offsetsConverter.BindLabelMapInfo(_labelMap);
        _offsetsConverter.SetBrushGeometry(BrushVm.GetBrushGeometryPort());

        // Pick list config
        _picker.SetTolerance(0.05);

        // Overlay ViewModels -----------------------------------------
        var bounds = Bounds.FromArray(_background.GetBounds());
        _axialCrosshairVm = CrosshairViewModel.Create(SliceOrientation.Axial, bounds);
        _coronalCrosshairVm = CrosshairViewModel.Create(SliceOrientation.Coronal, bounds);
        _sagittalCrosshairVm = CrosshairViewModel.Create(SliceOrientation.Sagittal, bounds);
        _obliqueBullseyeVm = BullseyeViewModel.Create(Double3.Zero, _obliqueVm.PlaneNormal);

        // Commands
        SetLabelOneVisibilityCommand = new DelegateCommand<bool?>(SetLabelOneVisibility);
        SetSliceOrientationCommand = new DelegateCommand(SetSliceOrientation);
    }


    public int AxialSliceIndex
    {
        get => _axialSliceIndex;
        set
        {
            if (value < 0 || value >= _backgroundDims[2]) return;
            this.RaiseAndSetIfChanged(ref _axialSliceIndex, value);
            SetSliceIndex(AxialVms, value);
        }
    }

    public int CoronalSliceIndex
    {
        get => _coronalSliceIndex;
        set
        {
            if (value < 0 || value >= _backgroundDims[1]) return;
            this.RaiseAndSetIfChanged(ref _coronalSliceIndex, value);
            SetSliceIndex(CoronalVms, value);
        }
    }

    public int SagittalSliceIndex
    {
        get => _sagittalSliceIndex;
        set
        {
            if (value < 0 || value >= _backgroundDims[0]) return;
            this.RaiseAndSetIfChanged(ref _sagittalSliceIndex, value);
            SetSliceIndex(SagittalVms, value);
        }
    }

    public int ObliqueSliceIndex
    {
        get => _obliqueSliceIndex;
        set
        {
            value = Math.Clamp(value, _obliqueVm.MinSliceIndex, _obliqueVm.MaxSliceIndex);
            this.RaiseAndSetIfChanged(ref _obliqueSliceIndex, value);
            SetSliceIndex(ObliqueVms, value);
        }
    }

    [Reactive] public byte LabelMapFillingValue { get; set; } = 1;

    public DelegateCommand<bool?> SetLabelOneVisibilityCommand { get; }
    public DelegateCommand SetSliceOrientationCommand { get; }


    public void OnControlGetMouseDisplayPosition(IVtkSceneControl sender, int x, int y)
    {
        if (_picker.Pick(x, y, 0, sender.MainRenderer) == 0) return;
        Double3 clickWorldPos = _picker.GetPickWorldPosition();

        if (_background.TryComputeStructuredCoordinates(clickWorldPos, out var voxel, out Double3 _))
        {
            AxialSliceIndex = voxel.k;
            CoronalSliceIndex = voxel.j;
            SagittalSliceIndex = voxel.i;
            _axialCrosshairVm.FocalPoint = clickWorldPos;
            _coronalCrosshairVm.FocalPoint = clickWorldPos;
            _sagittalCrosshairVm.FocalPoint = clickWorldPos;
        }
        if (_obliqueVm.TryWorldToSlice(clickWorldPos, out int sliceIdx, out double _, out double _))
        {
            ObliqueSliceIndex = sliceIdx;
            _obliqueBullseyeVm.FocalPoint = clickWorldPos;
        }
    }

    public void OnControlGetMousePaintPosition(IVtkSceneControl sender, int x, int y)
    {
        if (_picker.Pick(x, y, 0, sender.MainRenderer) == 0) return;

        Double3 clickWorldPos = _picker.GetPickWorldPosition();
        ReadOnlySpan<int> activeOffsets = _offsetsConverter.GetLinearOffsets();

        byte fillingValue = LabelMapFillingValue;
        _painter.PaintLinear(_labelMap, activeOffsets, clickWorldPos, fillingValue);
    }

    public void OnControlGetBrushPosition(IVtkSceneControl sender, int x, int y)
    {
        if (_picker.Pick(x, y, 0, sender.MainRenderer) == 0) return;

        Double3 clickWorldPos = _picker.GetPickWorldPosition();

        BrushVm.Center = clickWorldPos;
        BrushVm.Normal = sender.GetViewPlaneNormal();
    }

    private void SetLabelOneVisibility(bool? isVisible)
    {
        if (isVisible is null) return;

        double opacity = isVisible.Value ? LabelMapLookupTable.Opacity : 0.0;
        double[]? labelColor = _labelMapLut.GetColor(1); // take label==1 for example
        _labelMapLut.SetTableValue(1, labelColor[0], labelColor[1], labelColor[2], opacity);
        _labelMapLut.Modified();

        AxialVms[1].ForceRender();
    }

    private void SetSliceOrientation()
    {
        var sliceOrientation = Quaternion.CreateFromYawPitchRoll(Deg2Rad(YawDegrees),
            Deg2Rad(PitchDegrees),
            Deg2Rad(RollDegrees));

        _obliqueVm.SliceOrientation = sliceOrientation;
        _obliqueLabelVm.SliceOrientation = sliceOrientation;

        // Adjust overlays, so they can plot onto the resliced plane
        _obliqueBullseyeVm.Normal = _obliqueVm.PlaneNormal;
    }

    // ---- Helpers -----------------------------------------------
    private static void SetSliceIndex(IReadOnlyList<ImageSliceViewModel> vms, int sliceIndex)
    {
        foreach (var vm in vms)
        {
            vm.SliceIndex = sliceIndex;
        }
    }

    private static vtkImageData CreateLabelMap(vtkImageData refImage)
    {
        var dims = refImage.GetDimensions();
        var spacing = refImage.GetSpacing();
        var origin = refImage.GetOrigin();
        var labelMap = vtkImageData.New();
        labelMap.SetDimensions(dims[0], dims[1], dims[2]);
        labelMap.SetSpacing(spacing[0], spacing[1], spacing[2]);
        labelMap.SetOrigin(origin[0], origin[1], origin[2]);
        labelMap.SetScalarTypeToUnsignedChar();
        labelMap.SetNumberOfScalarComponents(1);
        labelMap.AllocateScalars();
        labelMap.ZeroScalars();
        return labelMap;
    }

    private static float Deg2Rad(float degrees) => (float)(degrees * Math.PI / 180);
}