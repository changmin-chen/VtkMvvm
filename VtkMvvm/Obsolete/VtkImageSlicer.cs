using Kitware.VTK;

namespace VtkMvvm.Obsolete;

/// <summary>
/// Helper slicer class 
/// </summary>
internal abstract class VtkImageSlicer : IDisposable
{
    protected readonly double[] Center;
    protected readonly double[] Origin;
    protected readonly double[] Spacing;
    private bool _disposed;

    protected VtkImageSlicer(vtkImageData image)
    {
        Origin = image.GetOrigin();
        Spacing = image.GetSpacing();
        Center = image.GetCenter();

        Reslice = new vtkImageReslice();
        Reslice.SetInput(image);
        Reslice.SetOutputDimensionality(2);
        Reslice.SetInterpolationModeToLinear();
    }

    protected vtkImageReslice Reslice { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public vtkAlgorithmOutput GetOutputPort() => Reslice.GetOutputPort();

    public abstract void SetSliceIndex(int sliceIndex);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) Reslice.Dispose();

            _disposed = true;
        }
    }
}

internal class VtkAxialSlicer : VtkImageSlicer
{
    public VtkAxialSlicer(vtkImageData image) : base(image)
    {
        // Orientation: X→right, Y→up, normal→+Z
        Reslice.SetResliceAxesDirectionCosines(
            1, 0, 0, // output X axis in input coords
            0, 1, 0, // output Y axis in input coords
            0, 0, 1 // slice normal (Z)
        );
    }

    public override void SetSliceIndex(int sliceIndex)
    {
        var worldZ = Origin[2] + sliceIndex * Spacing[2];
        Reslice.SetResliceAxesOrigin(Center[0], Center[1], worldZ);
        Reslice.Update();
    }
}

internal class VtkCoronalSlicer : VtkImageSlicer
{
    public VtkCoronalSlicer(vtkImageData image) : base(image)
    {
        // Orientation: X→right, Z→up, normal→–Y (so the "front" view)
        Reslice.SetResliceAxesDirectionCosines(
            1, 0, 0, // output X → input +X
            0, 0, 1, // output Y → input +Z
            0, -1, 0 // slice normal → –input Y
        );
    }

    public override void SetSliceIndex(int sliceIndex)
    {
        var worldY = Origin[1] + sliceIndex * Spacing[1];
        Reslice.SetResliceAxesOrigin(Center[0], worldY, Center[2]);
        Reslice.Update();
    }
}

internal class VtkSagittalSlicer : VtkImageSlicer
{
    public VtkSagittalSlicer(vtkImageData image) : base(image)
    {
        // Orientation: Z→right, Y→up, normal→+X (so a "side" view)
        Reslice.SetResliceAxesDirectionCosines(
            0, 0, 1, // output X → input +Z
            0, 1, 0, // output Y → input +Y
            1, 0, 0 // slice normal → +input X
        );
    }

    public override void SetSliceIndex(int sliceIndex)
    {
        var worldX = Origin[0] + sliceIndex * Spacing[0];
        Reslice.SetResliceAxesOrigin(worldX, Center[1], Center[2]);
        Reslice.Update();
    }
}