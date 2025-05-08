using System.Diagnostics;
using Kitware.VTK;

namespace VtkMvvm.Features.BrushPainter;

public class BrushToActiveOffsetsConvertor
{
    // For painting, we compute the active offset of the brush at (0,0,0)
    // millimeterToIjkFilter -> polyToStencil -> stencilToImage 
    private readonly vtkTransform _millimeterToIjk = new();

    private readonly vtkTransformPolyDataFilter _millimeterToIjkFilter = new();
    private readonly vtkImageStencilToImage _toMask = new();
    private readonly vtkPolyDataToImageStencil _toStencil = new();

    // Cache
    private ulong _cachedMTime = ulong.MaxValue; // “never seen”
    private List<(int dx, int dy, int dz)>? _cachedOffsets; // null => no cache yet

    public BrushToActiveOffsetsConvertor()
    {
        _millimeterToIjk.Identity();
        _millimeterToIjkFilter.SetTransform(_millimeterToIjk);

        _toStencil.SetOutputSpacing(1, 1, 1); // output to IJK space

        _toMask.SetInsideValue(1);
        _toMask.SetOutsideValue(0);
        _toMask.SetOutputScalarTypeToUnsignedChar();

        // Connect pipeline
        _toStencil.SetInputConnection(_millimeterToIjkFilter.GetOutputPort());
        _toMask.SetInputConnection(_toStencil.GetOutputPort());
    }

    /// <summary>
    ///     Set the input connection to the masking polyData output port.
    /// </summary>
    /// <param name="input">The port where its output should be <see cref="vtkPolyData" /> and in world space</param>
    public void SetBrushModelInputConnection(vtkAlgorithmOutput input)
    {
        _millimeterToIjkFilter.SetInputConnection(input);

        // tight bbox with a 1-voxel pad
        _millimeterToIjkFilter.Update();
        double[]? b = _millimeterToIjkFilter.GetOutput().GetBounds();
        Debug.WriteLine($"Brush bounds: {b[0]} {b[1]} {b[2]} {b[3]} {b[4]} {b[5]}");

        _toStencil.SetOutputWholeExtent(
            (int)Math.Floor(b[0]) - 1, (int)Math.Ceiling(b[1]) + 1,
            (int)Math.Floor(b[2]) - 1, (int)Math.Ceiling(b[3]) + 1,
            (int)Math.Floor(b[4]) - 1, (int)Math.Ceiling(b[5]) + 1);
    }

    /// <summary>
    ///     Because we are painting on the voxels of the labelMap. Spacing info is used for getting voxelized offsets
    /// </summary>
    /// <param name="sx">Spacing in millimeter at the first image axis</param>
    /// <param name="sy">Spacing in millimeter at the second image axis</param>
    /// <param name="sz">Spacing in millimeter at the third image axis</param>
    public void SetVoxelizeSpacing(double sx, double sy, double sz)
    {
        _millimeterToIjk.Identity();
        _millimeterToIjk.Scale(1.0 / sx, 1.0 / sy, 1.0 / sz); // number of the voxels drawn increased for thinner voxel 
        _millimeterToIjk.Modified();
    }

    public IReadOnlyList<(int dx, int dy, int dz)> GetActiveVoxelOffsets(double threshold = 0.5)
    {
        _toMask.UpdateInformation();

        // If we have already computed the offsets for exactly this image – reuse them.
        uint currentMTime = _toMask.GetOutput().GetPipelineMTime();
        if (currentMTime == _cachedMTime && _cachedOffsets is not null)
        {
            return _cachedOffsets!;
        }

        // Otherwise: (re-)compute.
        _toMask.Update();
        vtkImageData? mask = _toMask.GetOutput();
        int[]? ext = mask.GetExtent();
        List<(int dx, int dy, int dz)> list = _cachedOffsets ??= new List<(int dx, int dy, int dz)>();
        list.Clear();

        for (int z = ext[4]; z <= ext[5]; ++z)
        {
            for (int y = ext[2]; y <= ext[3]; ++y)
            {
                for (int x = ext[0]; x <= ext[1]; ++x)
                    if (mask.GetScalarComponentAsDouble(x, y, z, 0) > threshold)
                    {
                        list.Add((x, y, z));
                    }
            }
        }

        // update cache bookkeeping
        _cachedMTime = currentMTime;
        Debug.WriteLine($"Recompute offset at time: {_cachedMTime:X16} {DateTime.Now:O}");
        return list;
    }
}