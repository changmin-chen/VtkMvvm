using System.Runtime.CompilerServices;
using Kitware.VTK;
using VtkMvvm.Models;

namespace VtkMvvm.Extensions;

public static class ComputeIjkExtensions
{
    /// <summary>
    ///     Converts a world-space point to (i,j,k) voxel indices and parametric coords.
    /// </summary>
    /// <param name="image">The <see cref="vtkImageData" /> instance.</param>
    /// <param name="world">World coordinate in millimetres.</param>
    /// <param name="ijk">Integer voxel indices (only valid when the method returns <c>true</c>).</param>
    /// <param name="pcoords">Barycentric coordinates inside that voxel.</param>
    /// <returns><c>true</c> if <paramref name="world" /> lies inside the volume.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool TryComputeStructuredCoordinates(
        this vtkImageData image,
        in Double3 world,
        out (int i, int j, int k) ijk,
        out Double3 pcoords)
    {
        // stack-allocate the tiny scratch buffers → zero GC pressure
        double* xPtr = stackalloc double[3] { world.X, world.Y, world.Z };
        int* ijkPtr = stackalloc int[3];
        double* pcoordPtr = stackalloc double[3];

        int inside = image.ComputeStructuredCoordinates(
            (IntPtr)xPtr,
            (IntPtr)ijkPtr,
            (IntPtr)pcoordPtr);

        ijk = (ijkPtr[0], ijkPtr[1], ijkPtr[2]);
        pcoords = new Double3(
            pcoordPtr[0],
            pcoordPtr[1],
            pcoordPtr[2]);

        return inside == 1;
    }

    /// <summary>
    ///     Returns the last pick position in world coordinates.
    /// </summary>
    /// <remarks>
    ///     Call this only *after* a successful <c>Pick(..)</c>/<c>Pick3DPoint(..)</c> –
    ///     otherwise the result is undefined (VTK never initialises the array for you).
    /// </remarks>
    public static unsafe Double3 GetPickWorldPosition(this vtkAbstractPicker picker)
    {
        if (picker is null) throw new ArgumentNullException(nameof(picker));

        // Tiny, fixed-size scratch buffer on the stack → zero allocations.
        double* posPtr = stackalloc double[3];

        // Native VTK fills the buffer with x,y,z.
        picker.GetPickPosition((IntPtr)posPtr);

        // Convert to a nice, managed Vector3 and return.
        return new Double3(
            posPtr[0],
            posPtr[1],
            posPtr[2]);
    }
}