using Kitware.VTK;
using PresentationTest.TestData;
using ReactiveUI;
using VtkMvvm.Controls;
using VtkMvvm.Extensions;
using VtkMvvm.Features.BrushPainter;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;

namespace PresentationTest;

public class VtkMvvmTestWindowViewModel : ReactiveObject
{
    // Image data
    private readonly vtkImageData _background;

    // Brush
    private readonly vtkImageData _labelMap;
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
            .WithLinearInterpolation(false)
            .WithOpacity(1.0);
        _labelMap = CreateLabelMap(_background);
        vtkLookupTable labelLut = new();
        labelLut.SetNumberOfTableValues(256);
        labelLut.SetRange(0, 255);
        labelLut.SetTableValue(0, 0.0, 0.0, 0.0, 0.0); // label 0 transparent
        for (int i = 1; i < 256; ++i)
        {
            // simple “rainbow” – HSV -> RGB
            double h = i / 255.0; // hue 0-1
            (double r, double g, double b) = HsvToRgb(h, 1, 1);
            labelLut.SetTableValue(i, r, g, b, 0.3); // 30 % opacity
        }

        labelLut.Build();

        ColoredImagePipelineBuilder labelMapPipelineBuilder = ColoredImagePipelineBuilder
            .WithImage(_labelMap)
            .WithLinearInterpolation(false)
            .WithPickable(false)
            .WithRgbaLookupTable(labelLut);

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
        // _offsetsConverter.SetVoxelizeSpacing(spacing[0], spacing[1], spacing[2]);
        _offsetsConverter.BindLabelMapInfo(_labelMap);
        _offsetsConverter.SetBrushGeometry(BrushVm.GetBrushGeometryPort());

        // Pick list
        _picker.SetTolerance(0.005);
        _picker.PickFromListOn();
        _picker.AddPickList(axialVm.Actor);
        _picker.AddPickList(coronalVm.Actor);
        _picker.AddPickList(sagittalVm.Actor);
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
        // ReadOnlySpan<(int dx, int dy, int dz)> activeOffsets = _offsetsConverter.GetActiveVoxelOffsets();
        ReadOnlySpan<int> activeOffsets = _offsetsConverter.GetLinearOffsets();

        // _painter.Paint(_labelMap, activeOffsets, clickWorldPos, 1);
        _painter.PaintLinear(_labelMap, activeOffsets, clickWorldPos, 1);
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


    /// <summary>
    /// Convert an HSV color (hue, saturation, value) to RGB.
    /// h, s, v ∈ [0,1]. Returns r, g, b ∈ [0,1].
    /// </summary>
    private static (double r, double g, double b) HsvToRgb(double h, double s, double v)
    {
        // 1. If sat=0, it's a gray (achromatic) color: r=g=b=v
        if (s == 0)
        {
            return (v, v, v);
        }

        // 2. Hue sector: scale hue to [0,6), then take integer + fractional part
        double sector = h * 6.0;
        int i = (int)Math.Floor(sector); // sector index: 0..5
        double f = sector - i; // fractional part within sector

        // 3. Precompute intermediate p, q, t values
        double p = v * (1.0 - s);
        double q = v * (1.0 - s * f);
        double t = v * (1.0 - s * (1 - f));

        // 4. Select the correct RGB case based on sector index
        switch (i % 6)
        {
            case 0: return (v, t, p); // red→yellow
            case 1: return (q, v, p); // yellow→green
            case 2: return (p, v, t); // green→cyan
            case 3: return (p, q, v); // cyan→blue
            case 4: return (t, p, v); // blue→magenta
            case 5: return (v, p, q); // magenta→red
            default: // should never happen
                return (0, 0, 0);
        }
    }
}