using itk.simple;
using Kitware.VTK;
using MedXtend;
using MedXtend.Itk;
using PresentationTest.Constants;
using PresentationTest.Extensions;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using VtkMvvm.Controls;
using VtkMvvm.Extensions;
using VtkMvvm.Features.BrushPainter;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;

namespace PresentationTest.ViewModels;

public class VtkMvvmTestWindowViewModel : ReactiveObject
{
    // Go with image data
    private readonly vtkImageData _background;
    private readonly int[] _backgroundDims;

    // Overlay: crosshairs, slice-labels, brush
    private readonly CrosshairViewModel _axialCrosshairVm, _coronalCrosshairVm, _sagittalCrosshairVm;
    private readonly OrientationLabelsViewModel _axialSliceLabel, _coronalSliceLabel, _sagittalSliceLabel;

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


    public VtkMvvmTestWindowViewModel()
    {
        // _background = TestImageLoader.ReadNifti(@"TestData\CT_Abdo.nii.gz");
        using var itkImage = SimpleITK.ReadImage(@"TestData\CT_Abdo.nii.gz");
        using var itkOriented = itkImage.ReorientToIdentityPhysicalEquivalent();
        var vtkOriented = itkOriented.ToOrientedVtk();
        _background = vtkOriented;

        _backgroundDims = _background.GetDimensions();

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

        var axialVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Axial, bgPipe);
        var labelAxialVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Axial, labelMapPipe);
        AxialVms = [axialVm, labelAxialVm];

        var coronalVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Coronal, bgPipe);
        var labelCoronalVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Coronal, labelMapPipe);
        CoronalVms = [coronalVm, labelCoronalVm];

        var sagittalVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Sagittal, bgPipe);
        var labelSagittalVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Sagittal, labelMapPipe);
        SagittalVms = [sagittalVm, labelSagittalVm];

        // Add brushes that render on top of the image
        BrushVm.Diameter = 3.0;

        // Instantiate voxel-brush and cached
        double[]? spacing = _labelMap.GetSpacing();
        BrushVm.Height = spacing.Min();
        _offsetsConverter.BindLabelMapInfo(_labelMap);
        _offsetsConverter.SetBrushGeometry(BrushVm.GetBrushGeometryPort());

        // Pick list
        _picker.SetTolerance(0.005);
        _picker.PickFromListOn();
        _picker.AddPickList(axialVm.Actor);
        _picker.AddPickList(coronalVm.Actor);
        _picker.AddPickList(sagittalVm.Actor);

        // Overlay ViewModels: Crosshair and Brush
        var bounds = Bounds.FromArray(_background.GetBounds());
        _axialCrosshairVm = CrosshairViewModel.Create(SliceOrientation.Axial, bounds);
        _coronalCrosshairVm = CrosshairViewModel.Create(SliceOrientation.Coronal, bounds);
        _sagittalCrosshairVm = CrosshairViewModel.Create(SliceOrientation.Sagittal, bounds);
        _axialSliceLabel = OrientationLabelsViewModel.Create();
        _coronalSliceLabel = OrientationLabelsViewModel.Create();
        _sagittalSliceLabel = OrientationLabelsViewModel.Create();
        _axialSliceLabel.UpdateFromOrientation(SliceOrientation.Axial);
        _coronalSliceLabel.UpdateFromOrientation(SliceOrientation.Coronal);
        _sagittalSliceLabel.UpdateFromOrientation(SliceOrientation.Sagittal);
        AxialOverlayVms = [BrushVm, _axialCrosshairVm, _axialSliceLabel];
        CoronalOverlayVms = [BrushVm, _coronalCrosshairVm, _coronalSliceLabel];
        SagittalOverlayVms = [BrushVm, _sagittalCrosshairVm, _sagittalSliceLabel];

        // Commands
        SetLabelOneVisibilityCommand = new DelegateCommand<bool?>(SetLabelOneVisibility);
    }


    // Background ViewModels: Axial, Coronal, Sagittal slice view models
    public ImageOrthogonalSliceViewModel[] AxialVms { get; }
    public ImageOrthogonalSliceViewModel[] CoronalVms { get; }
    public ImageOrthogonalSliceViewModel[] SagittalVms { get; }

    // Overlay ViewModels
    public BrushViewModel BrushVm { get; } = new(); // directly binds to slider for setting diameter
    public VtkElementViewModel[] AxialOverlayVms { get; }
    public VtkElementViewModel[] CoronalOverlayVms { get; }
    public VtkElementViewModel[] SagittalOverlayVms { get; }

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

    [Reactive] public byte LabelMapFillingValue { get; set; } = 1;

    public DelegateCommand<bool?> SetLabelOneVisibilityCommand { get; }

    private void SetLabelOneVisibility(bool? isVisible)
    {
        if (isVisible is null) return;

        double opacity = isVisible.Value ? LabelMapLookupTable.Opacity : 0.0;
        double[]? labelColor = _labelMapLut.GetColor(1); // take label==1 for example
        _labelMapLut.SetTableValue(1, labelColor[0], labelColor[1], labelColor[2], opacity);
        _labelMapLut.Modified();

        AxialVms[1].ForceRender();
    }

    public void OnControlGetMouseDisplayPosition(VtkImageSceneControl sender, int x, int y)
    {
        if (_picker.Pick(x, y, 0, sender.MainRenderer) == 0) return;

        Double3 clickWorldPos = _picker.GetPickWorldPosition();
        if (_background.TryComputeStructuredCoordinates(clickWorldPos, out (int i, int j, int k) voxel, out Double3 _))
        {
            AxialSliceIndex = voxel.k;
            CoronalSliceIndex = voxel.j;
            SagittalSliceIndex = voxel.i;

            _axialCrosshairVm.FocalPoint = clickWorldPos;
            _coronalCrosshairVm.FocalPoint = clickWorldPos;
            _sagittalCrosshairVm.FocalPoint = clickWorldPos;
        }
    }

    public void OnControlGetMousePaintPosition(VtkImageSceneControl sender, int x, int y)
    {
        if (_picker.Pick(x, y, 0, sender.MainRenderer) == 0) return;

        Double3 clickWorldPos = _picker.GetPickWorldPosition();
        ReadOnlySpan<int> activeOffsets = _offsetsConverter.GetLinearOffsets();

        byte fillingValue = LabelMapFillingValue;
        _painter.PaintLinear(_labelMap, activeOffsets, clickWorldPos, fillingValue);
    }

    public void OnControlGetBrushPosition(VtkImageSceneControl sender, int x, int y)
    {
        if (_picker.Pick(x, y, 0, sender.MainRenderer) == 0) return;

        Double3 clickWorldPos = _picker.GetPickWorldPosition();

        BrushVm.Center = clickWorldPos;
        BrushVm.Orientation = sender.Orientation;
    }

    private static void SetSliceIndex(IEnumerable<ImageOrthogonalSliceViewModel> vms, int sliceIndex)
    {
        foreach (ImageOrthogonalSliceViewModel vm in vms) vm.SliceIndex = sliceIndex;
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
}