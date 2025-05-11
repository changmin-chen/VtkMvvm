using Kitware.VTK;
using PresentationTest.Constants;
using PresentationTest.TestData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using VtkMvvm.Controls;
using VtkMvvm.Extensions;
using VtkMvvm.Features.BrushPainter;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;

namespace PresentationTest;

public class VtkMvvmTestWindowViewModel : ReactiveObject
{
    // Go with image data
    private readonly vtkImageData _background;

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
        _background = TestImageLoader.ReadNifti(@"TestData\CT_Abdo.nii.gz");

        ColoredImagePipelineBuilder backgroundPipelineBuilder = ColoredImagePipelineBuilder
            .WithImage(_background)
            .WithLinearInterpolation(true)
            .WithOpacity(1.0);
        _labelMap = CreateLabelMap(_background);


        ColoredImagePipelineBuilder labelMapPipelineBuilder = ColoredImagePipelineBuilder
            .WithImage(_labelMap)
            .WithLinearInterpolation(false)
            .WithPickable(false)
            .WithRgbaLookupTable(_labelMapLut);

        ImageOrthogonalSliceViewModel axialVm = new(SliceOrientation.Axial, backgroundPipelineBuilder.Build());
        ImageOrthogonalSliceViewModel labelAxialVm = new(SliceOrientation.Axial, labelMapPipelineBuilder.Build());
        AxialVms = [axialVm, labelAxialVm];

        ImageOrthogonalSliceViewModel coronalVm = new(SliceOrientation.Coronal, backgroundPipelineBuilder.Build());
        ImageOrthogonalSliceViewModel labelCoronalVm = new(SliceOrientation.Coronal, labelMapPipelineBuilder.Build());
        CoronalVms = [coronalVm, labelCoronalVm];

        ImageOrthogonalSliceViewModel sagittalVm = new(SliceOrientation.Sagittal, backgroundPipelineBuilder.Build());
        ImageOrthogonalSliceViewModel labelSagittalVm = new(SliceOrientation.Sagittal, labelMapPipelineBuilder.Build());
        SagittalVms = [sagittalVm, labelSagittalVm];

        // Add brushes that render on top of the image
        BrushVm.Diameter = 3.0;
        BrushSharedVms = [BrushVm];

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

        // Commands
        SetLabelOneVisibilityCommand = new DelegateCommand<bool?>(SetLabelOneVisibility);
    }


    // Axial, Coronal, Sagittal slice view models
    public ImageOrthogonalSliceViewModel[] AxialVms { get; }
    public ImageOrthogonalSliceViewModel[] CoronalVms { get; }
    public ImageOrthogonalSliceViewModel[] SagittalVms { get; }

    public BrushViewModel BrushVm { get; } = new();
    public VtkElementViewModel[] BrushSharedVms { get; }

    public int AxialSliceIndex
    {
        get => _axialSliceIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _axialSliceIndex, value);
            SetSliceIndex(AxialVms, value);
        }
    }

    public int CoronalSliceIndex
    {
        get => _coronalSliceIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _coronalSliceIndex, value);
            SetSliceIndex(CoronalVms, value);
        }
    }

    public int SagittalSliceIndex
    {
        get => _sagittalSliceIndex;
        set
        {
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