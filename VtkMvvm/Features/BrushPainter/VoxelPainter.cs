using System.Runtime.CompilerServices;
using Kitware.VTK;
using VtkMvvm.Extensions;
using VtkMvvm.Models;

namespace VtkMvvm.Features.BrushPainter;

public sealed unsafe class VoxelPainter
{
    // Cache last-used labelMap to avoid repeated reflection & bounds look-ups
    private vtkImageData? _cachedVolume;
    private byte* _dataPtr;
    private int _dimX, _dimY, _dimZ, _voxPerSlice;

    /// <summary>
    ///     Paint the supplied <paramref name="brushOffsets" /> into <paramref name="labelMap" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Paint(
        vtkImageData labelMap,
        ReadOnlySpan<(int dx, int dy, int dz)> brushOffsets, // pre-computed, int linear = z * _voxPerSlice + y * _dimX + x
        Double3 worldCentre,
        byte labelValue = 255)
    {
        // ------- 0. Cache (once per volume) ----------------------------
        if (!ReferenceEquals(labelMap, _cachedVolume)) InitializeCache(labelMap);

        // ------- 1. Stamp the brush at the centre ---------------------
        if (!labelMap.TryComputeStructuredCoordinates(worldCentre, out (int i, int j, int k) c, out _))
            return; // ijk outside the volume

        // ----- stamp the brush -----
        int centreIdx = c.k * _voxPerSlice + c.j * _dimX + c.i;
        int sliceStride = _voxPerSlice; // dimX * dimY
        int rowStride = _dimX;
        int volumeSize = _dimX * _dimY * _dimZ;

        byte* basePtr = _dataPtr;

        foreach ((int dx, int dy, int dz) in brushOffsets)
        {
            int idx = centreIdx + dz * sliceStride + dy * rowStride + dx;
            if ((uint)idx < (uint)volumeSize) // single bounds check
                basePtr[idx] = labelValue;
        }

        labelMap.Modified();
    }


    private void InitializeCache(vtkImageData labelMap)
    {
        _cachedVolume = labelMap;

        int[]? dims = labelMap.GetDimensions();
        _dimX = dims[0];
        _dimY = dims[1];
        _dimZ = dims[2];
        _voxPerSlice = _dimX * _dimY;

        _dataPtr = (byte*)labelMap.GetScalarPointer().ToPointer();
    }
}