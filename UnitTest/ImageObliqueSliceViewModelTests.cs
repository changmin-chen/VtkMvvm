using System.Numerics;
using Kitware.VTK;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;

namespace UnitTest;


[Collection("VTK")]
public sealed class ImageObliqueSliceViewModelTests : IDisposable
{
    private readonly vtkImageData _volume;
    private readonly ImageObliqueSliceViewModel _vm;

    public ImageObliqueSliceViewModelTests()
    {
        _volume = MakeDummyVolume(dim: 100, spacing: 1.0); // 100³, 1 mm/voxel

        var pipeline = ColoredImagePipelineBuilder.WithSharedImage(_volume).Build();
        _vm = new ImageObliqueSliceViewModel(Quaternion.Identity, pipeline);
    }

    // ────────────────────────── Basic parameter validation ──────────────────────────

    [Fact(DisplayName = "Step = 1 mm for axial orientation")]
    public void StepIsOneMillimeter_ForAxial()
        => Assert.Equal(1.0, _vm.StepMillimeter, precision: 6);

    [Fact(DisplayName = "Slider range = ±(Z-1)/2")]
    public void SliderRangeMatchesVolumeDepth()
    {
        int depth = _volume.GetDimensions()[2]; // 100
        int expectedHalf = (depth - 1) / 2; // 49
        Assert.Equal(-expectedHalf, _vm.MinSliceIndex);
        Assert.Equal(expectedHalf, _vm.MaxSliceIndex);
    }

    // ────────────────────────── TryWorldToSlice() ──────────────────────────

    [Fact(DisplayName = "World centre ⇀ idx=0, i=j≈0")]
    public void TryWorldToSlice_CentreGivesZeroIndex()
    {
        Double3 centre = ToDouble3(_volume.GetCenter()); // (49.5,49.5,49.5)
        bool ok = _vm.TryWorldToSlice(centre, out int idx, out double i, out double j);

        Assert.True(ok);
        Assert.Equal(0, idx); // Center → slice 0
        Assert.InRange(i, -1e-6, 1e-6); // Also at image center
        Assert.InRange(j, -1e-6, 1e-6);
    }

    [Fact(DisplayName = "+10 mm along normal ⇀ idx+10")]
    public void TryWorldToSlice_AlongNormalIncrementsIndex()
    {
        Double3 centre = ToDouble3(_volume.GetCenter());
        Double3 p = centre with { Z = centre.Z + 10 }; // axial normal = +Z
        bool ok = _vm.TryWorldToSlice(p, out int idx, out _, out _);

        Assert.True(ok);
        Assert.Equal(10, idx);
    }

    [Fact(DisplayName = "Point outside volume ⇀ false")]
    public void TryWorldToSlice_ReturnsFalseWhenOutside()
    {
        // Point located outside +2×depth
        Double3 p = new(0, 0, _volume.GetBounds()[5] + 100);
        bool ok = _vm.TryWorldToSlice(p, out _, out _, out _);
        Assert.False(ok);
    }

    // ────────────────────────── Rotation changes ──────────────────────────

    [Fact(DisplayName = "90° pitch → normal = +Y, slider range changes to ±(Height-1)/2")]
    public void ChangingOrientation_UpdatesInternals()
    {
        // 90° rotation (around +X): axial → sagittal
        var q = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
        _vm.SliceOrientation = q;

        // Step is still 1 mm (spacing Y)
        Assert.Equal(1.0, _vm.StepMillimeter, precision: 6);

        // Slider now looks at Y height
        int height = _volume.GetDimensions()[1]; // 100
        int expectedHalf = (height - 1) / 2; // 49
        Assert.Equal(-expectedHalf, _vm.MinSliceIndex);
        Assert.Equal(expectedHalf, _vm.MaxSliceIndex);

        // Normal should be -Y
        Vector3 expectedN = - Vector3.UnitY;  
        Vector3 diff = expectedN - _vm.PlaneNormal;
        Assert.True(diff.Length() < 1e-6);
    }


    // ────────────────────────── helpers & teardown ──────────────────────────
    private static vtkImageData MakeDummyVolume(int dim, double spacing)
    {
        var img = vtkImageData.New();
        img.SetDimensions(dim, dim, dim);
        img.SetSpacing(spacing, spacing, spacing);
        img.SetOrigin(0, 0, 0);
        img.SetScalarTypeToShort();
        img.AllocateScalars();
        // Fill with arbitrary values: we don't need actual content here, just meaningful scalar range
        Enumerable.Range(0, (int)img.GetNumberOfPoints())
            .ToList()
            .ForEach(i => img.GetPointData().GetScalars().SetTuple1(i, 100));
        img.Modified();
        return img;
    }

    private static Double3 ToDouble3(double[] v) => new(v[0], v[1], v[2]);

    public void Dispose()
    {
        _vm.Dispose();
        _volume.Dispose();
    }
}