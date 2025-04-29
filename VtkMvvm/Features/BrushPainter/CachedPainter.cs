using Kitware.VTK;
using VtkMvvm.Extensions;

namespace VtkMvvm.Features.BrushPainter;

public sealed unsafe class CachedPainter
{
    // Cache last-used labelMap to avoid repeated reflection & bounds look-ups
    private vtkImageData? _cachedVolume;
    private byte* _dataPtr;
    private int _dimX, _dimY, _dimZ, _voxPerSlice;
    private vtkMatrix4x4? _worldToIjk;

    /// <summary>
    /// Paint the supplied <paramref name="brush"/> into <paramref name="labelMap"/>.
    /// </summary>
    public void Paint(
        vtkImageData labelMap,
        VoxelBrush brush,
        IEnumerable<double[]> worldCentres,
        byte labelValue = 255)
    {
        // ------- 0. Cache (once per volume) ----------------------------
        if (!ReferenceEquals(labelMap, _cachedVolume)) InitializeCache(labelMap);
        if (_worldToIjk == null) throw new InvalidOperationException("Matrix missing");

        // ------- 1. Stamp the brush at every centre ---------------------
        foreach (var wc in worldCentres)
        {
            // transform WC -> IJK
            double i = _worldToIjk.GetElement(0, 0) * wc[0] + _worldToIjk.GetElement(0, 1) * wc[1] +
                       _worldToIjk.GetElement(0, 2) * wc[2] + _worldToIjk.GetElement(0, 3);
            double j = _worldToIjk.GetElement(1, 0) * wc[0] + _worldToIjk.GetElement(1, 1) * wc[1] +
                       _worldToIjk.GetElement(1, 2) * wc[2] + _worldToIjk.GetElement(1, 3);
            double k = _worldToIjk.GetElement(2, 0) * wc[0] + _worldToIjk.GetElement(2, 1) * wc[1] +
                       _worldToIjk.GetElement(2, 2) * wc[2] + _worldToIjk.GetElement(2, 3);

            int cX = (int)Math.Round(i);
            int cY = (int)Math.Round(j);
            int cZ = (int)Math.Round(k);

            // ----- stamp the brush -----
            foreach (var (dx, dy, dz) in brush.Offsets)
            {
                int x = cX + dx;
                int y = cY + dy;
                int z = cZ + dz;

                if (x < 0 || x >= _dimX ||
                    y < 0 || y >= _dimY ||
                    z < 0 || z >= _dimZ) continue;

                int linear = (z * _voxPerSlice) + (y * _dimX) + x;
                _dataPtr[linear] = labelValue;
            }
        }

        labelMap.Modified();
    }

    public void PaintParallel(
        vtkImageData labelMap,
        VoxelBrush brush,
        IReadOnlyList<double[]> worldCentres,
        byte labelValue = 255,
        int? maxDegree = null)
    {
        // ------- 0. Cache (once per volume) ----------------------------
        if (!ReferenceEquals(labelMap, _cachedVolume)) InitializeCache(labelMap);
        if (_worldToIjk == null) throw new InvalidOperationException("Matrix missing");

        // 1. Local copies to keep the lambda capture cheap
        var mat = _worldToIjk; // immutable – safe
        int dimX = _dimX;
        int dimY = _dimY;
        int dimZ = _dimZ;
        int voxPerSlice = _voxPerSlice;
        byte* basePtr = _dataPtr; // unmanaged, so stable across threads
        var offsets = brush.Offsets; // small struct list

        var opts = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegree ?? Environment.ProcessorCount
        };

        Parallel.For(0, worldCentres.Count, opts, idx =>
        {
            var wc = worldCentres[idx];

            // ----- transform WC -> IJK (same maths as before) -----
            double i = mat.GetElement(0, 0) * wc[0] + mat.GetElement(0, 1) * wc[1] +
                       mat.GetElement(0, 2) * wc[2] + mat.GetElement(0, 3);
            double j = mat.GetElement(1, 0) * wc[0] + mat.GetElement(1, 1) * wc[1] +
                       mat.GetElement(1, 2) * wc[2] + mat.GetElement(1, 3);
            double k = mat.GetElement(2, 0) * wc[0] + mat.GetElement(2, 1) * wc[1] +
                       mat.GetElement(2, 2) * wc[2] + mat.GetElement(2, 3);

            int cX = (int)Math.Round(i);
            int cY = (int)Math.Round(j);
            int cZ = (int)Math.Round(k);

            // ----- stamp the brush -----
            foreach (var (dx, dy, dz) in offsets)
            {
                int x = cX + dx;
                int y = cY + dy;
                int z = cZ + dz;

                if ((uint)x >= dimX || (uint)y >= dimY || (uint)z >= dimZ) continue;

                int linear = (z * voxPerSlice) + (y * dimX) + x;
                basePtr[linear] = labelValue; // data race is benign: identical byte
            }
        });

        labelMap.Modified();
    }

    private void InitializeCache(vtkImageData labelMap)
    {
        _cachedVolume = labelMap;

        var dims = labelMap.GetDimensions();
        _dimX = dims[0];
        _dimY = dims[1];
        _dimZ = dims[2];
        _voxPerSlice = _dimX * _dimY;

        _dataPtr = (byte*)labelMap.GetScalarPointer().ToPointer();
        _worldToIjk = labelMap.GetWorldToIjkTransform();
    }
}