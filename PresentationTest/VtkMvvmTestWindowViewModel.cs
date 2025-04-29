using System.Collections.ObjectModel;
using Kitware.VTK;
using MedXtend;
using MedXtend.Vtk.ImageData;
using PresentationTest.TestData;
using VtkMvvm.Features.BrushPainter;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;

namespace PresentationTest;

public class VtkMvvmTestWindowViewModel : BindableBase
{
    // Image data
    private readonly vtkImageData _background;
    private readonly vtkImageData _labelMap;

    // Axial, Coronal, Sagittal slice view models
    public ObservableCollection<ImageOrthogonalSliceViewModel> AxialVms { get; } = new();
    public ObservableCollection<ImageOrthogonalSliceViewModel> CoronalVms { get; } = new();
    public ObservableCollection<ImageOrthogonalSliceViewModel> SagittalVms { get; } = new();

    // Painting labelmap
    private readonly CachedPainter _painter = new();
    private readonly VoxelCylinderBrush _brushAxial; // cached brush for performance
    private readonly VoxelCylinderBrush _brushSagittal;
    private readonly VoxelCylinderBrush _brushCoronal;

    private const double BrushHeight = 1.0;
    private const double BrushDiameter = 1.5;


    public VtkMvvmTestWindowViewModel()
    {
        using var imageItk = TestImageLoader.LoadEmbeddedTestImage("big_dog_mri.nii");
        _background = imageItk.ToOrientedVtk();

        var backgroundPipelineBuilder = ColoredImagePipelineBuilder
            .WithImage(_background)
            .WithOpacity(1.0)
            .WithLinearInterpolation(true);

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
            labelLut.SetTableValue(i, r, g, b, 0.7); // 70 % opacity
        }

        labelLut.Build();

        var labelMapPipelineBuilder = ColoredImagePipelineBuilder
            .WithImage(_labelMap)
            .WithPickable(false)
            .WithRgbaLookupTable(labelLut);

        var axialVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Axial, backgroundPipelineBuilder.Build());
        var labelAxialVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Axial, labelMapPipelineBuilder.Build());
        AxialVms.AddRange([axialVm, labelAxialVm]);

        var coronalVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Coronal, backgroundPipelineBuilder.Build());
        var labelCoronalVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Coronal, labelMapPipelineBuilder.Build());
        CoronalVms.AddRange([coronalVm, labelCoronalVm]);

        var sagittalVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Sagittal, backgroundPipelineBuilder.Build());
        var labelSagittalVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Sagittal, labelMapPipelineBuilder.Build());
        SagittalVms.AddRange([sagittalVm, labelSagittalVm]);

        // Instantiate voxel-brush and cached
        var spacing = _labelMap.GetSpacing();
        _brushAxial = VoxelCylinderBrush.Create(
            (spacing[0], spacing[1], spacing[2]),
            diameterMm: BrushDiameter,
            heightMm: BrushHeight,
            axis: VoxelCylinderBrush.Axis.Z
        );
        _brushCoronal = VoxelCylinderBrush.Create(
            (spacing[0], spacing[1], spacing[2]),
            diameterMm: BrushDiameter,
            heightMm: BrushHeight,
            axis: VoxelCylinderBrush.Axis.Y
        );
        _brushSagittal = VoxelCylinderBrush.Create(
            (spacing[0], spacing[1], spacing[2]),
            diameterMm: BrushDiameter,
            heightMm: BrushHeight,
            axis: VoxelCylinderBrush.Axis.X
        );
    }

    private int _axialSliceIndex;
    private int _coronalSliceIndex;
    private int _sagittalSliceIndex;

    public int AxialSliceIndex
    {
        get => _axialSliceIndex;
        set
        {
            if (SetProperty(ref _axialSliceIndex, value)) SetSliceIndex(AxialVms, value);
        }
    }

    public int CoronalSliceIndex
    {
        get => _coronalSliceIndex;
        set
        {
            if (SetProperty(ref _coronalSliceIndex, value)) SetSliceIndex(CoronalVms, value);
        }
    }

    public int SagittalSliceIndex
    {
        get => _sagittalSliceIndex;
        set
        {
            if (SetProperty(ref _sagittalSliceIndex, value)) SetSliceIndex(SagittalVms, value);
        }
    }

    /// <summary>
    /// Should be called by View 
    /// </summary>
    public void PaintLabelMap(SliceOrientation orientation, IReadOnlyList<double[]> worldCentres)
    {
        // debug...
        // var o = _labelMap.GetOrigin();
        // var s = _labelMap.GetSpacing();
        // Debug.WriteLine($"origin (x, y, z) = ({o[0]}, {o[1]}, {o[2]})");
        // Debug.WriteLine($"spacing (x, y, z) = ({s[0]}, {s[1]}, {s[2]})");
        // int idx = 0;
        // foreach (var wc in worldCentres)
        // {
        //     Debug.WriteLine($"point-{idx} (x, y, z) = ({wc[0]}, {wc[1]}, {wc[2]})");
        //     idx++;
        // }

        const int activeLabel = 123;
        switch (orientation)
        {
            case SliceOrientation.Axial:
                _painter.PaintParallel(_labelMap, _brushAxial, worldCentres, activeLabel);
                break;
            case SliceOrientation.Coronal:
                _painter.PaintParallel(_labelMap, _brushCoronal, worldCentres, activeLabel);
                break;
            case SliceOrientation.Sagittal:
                _painter.PaintParallel(_labelMap, _brushSagittal, worldCentres, activeLabel);
                break;
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

    private static void SetSliceIndex(IEnumerable<ImageOrthogonalSliceViewModel> vms, int sliceIndex)
    {
        foreach (var vm in vms)
            vm.SliceIndex = sliceIndex;
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