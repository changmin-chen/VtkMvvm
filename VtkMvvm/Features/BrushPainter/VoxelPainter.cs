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
    ///     Paint the supplied <paramref name="brushVoxelOffsets" /> into <paramref name="labelMap" />.
    /// </summary>
    public void Paint(
        vtkImageData labelMap,
        IReadOnlyCollection<(int dx, int dy, int dz)> brushVoxelOffsets,
        IEnumerable<Double3> worldCentres,
        byte labelValue = 255)
    {
        // ------- 0. Cache (once per volume) ----------------------------
        if (!ReferenceEquals(labelMap, _cachedVolume)) InitializeCache(labelMap);

        // ------- 1. Stamp the brush at every centre ---------------------
        foreach (Double3 wc in worldCentres)
        {
            if (!labelMap.TryComputeStructuredCoordinates(wc, out (int i, int j, int k) cVoxel, out _))
                continue; // ijk outside the volume

            int cX = cVoxel.i;
            int cY = cVoxel.j;
            int cZ = cVoxel.k;

            // ----- stamp the brush -----
            foreach ((int dx, int dy, int dz) in brushVoxelOffsets)
            {
                int x = cX + dx;
                int y = cY + dy;
                int z = cZ + dz;

                if (x < 0 || x >= _dimX ||
                    y < 0 || y >= _dimY ||
                    z < 0 || z >= _dimZ) continue;

                int linear = z * _voxPerSlice + y * _dimX + x;
                _dataPtr[linear] = labelValue;
            }
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