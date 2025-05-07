using System.Numerics;
using Kitware.VTK;
using MedXtend;
using MedXtend.Vtk.ImageData;
using PresentationTest.TestData;
using ReactiveUI;
using VtkMvvm.Controls;
using VtkMvvm.Features.BrushPainter;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;
using Image = itk.simple.Image;

namespace PresentationTest;

public class VtkMvvmTestWindowViewModel : ReactiveObject
{
    // Image data
    private readonly vtkImageData _background;

    // Brush
    private readonly VoxelCylinderBrush _brushAxial; // cached brush for performance
    private readonly VoxelCylinderBrush _brushCoronal;
    private readonly VoxelCylinderBrush _brushSagittal;
    private readonly vtkImageData _labelMap;
    private readonly CachedPainter _painter = new();

    // Painting labelmap
    private readonly vtkCellPicker _picker = new();
    private int _axialSliceIndex;
    private int _coronalSliceIndex;
    private int _sagittalSliceIndex;


    public VtkMvvmTestWindowViewModel()
    {
        using Image imageItk = TestImageLoader.LoadEmbeddedTestImage("big_dog_mri.nii");
        _background = imageItk.ToOrientedVtk();

        ColoredImagePipelineBuilder backgroundPipelineBuilder = ColoredImagePipelineBuilder
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

        ColoredImagePipelineBuilder labelMapPipelineBuilder = ColoredImagePipelineBuilder
            .WithImage(_labelMap)
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
        BrushVm.Diameter = 2.0;
        BrushVm.Height = 2.0;
        BrushSharedVms = [BrushVm];

        // Instantiate voxel-brush and cached
        double[]? spacing = _labelMap.GetSpacing();
        _brushAxial = VoxelCylinderBrush.Create(
            (spacing[0], spacing[1], spacing[2]),
            BrushVm.Diameter,
            BrushVm.Height
        );
        _brushCoronal = VoxelCylinderBrush.Create(
            (spacing[0], spacing[1], spacing[2]),
            BrushVm.Diameter,
            BrushVm.Height,
            VoxelCylinderBrush.Axis.Y
        );
        _brushSagittal = VoxelCylinderBrush.Create(
            (spacing[0], spacing[1], spacing[2]),
            BrushVm.Diameter,
            BrushVm.Height,
            VoxelCylinderBrush.Axis.X
        );
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

        Vector3 clickWorldPos = _picker.GetPickWorldPosition();
        if (_background.TryComputeStructuredCoordinates(clickWorldPos, out (int i, int j, int k) voxel, out Vector3 bary))
        {
            AxialSliceIndex = voxel.k;
            CoronalSliceIndex = voxel.j;
            SagittalSliceIndex = voxel.i;
        }
    }

    public void OnControlGetMousePaintPosition(VtkImageSceneControl sender, int x, int y)
    {
        if (_picker.Pick(x, y, 0, sender.MainRenderer) == 0) return;

        Vector3 clickWorldPos = _picker.GetPickWorldPosition();
        double[] centre = [clickWorldPos.X, clickWorldPos.Y, clickWorldPos.Z];

        switch (sender.Orientation)
        {
            case SliceOrientation.Axial:
                _painter.Paint(_labelMap, _brushAxial!, [centre], 1); // TODO: should support 1 centre draw
                break;
            case SliceOrientation.Coronal:
                _painter.Paint(_labelMap, _brushCoronal!, [centre], 1);
                break;
            case SliceOrientation.Sagittal:
                _painter.Paint(_labelMap, _brushSagittal!, [centre], 1);
                break;
        }
    }

    public void OnControlGetBrushPosition(VtkImageSceneControl sender, int x, int y)
    {
        if (_picker.Pick(x, y, 0, sender.MainRenderer) == 0) return;

        Vector3 clickWorldPos = _picker.GetPickWorldPosition();
        BrushVm.SetCenter(clickWorldPos.X, clickWorldPos.Y, clickWorldPos.Z);
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