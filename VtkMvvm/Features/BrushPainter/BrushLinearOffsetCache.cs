using System.Runtime.InteropServices;
using Kitware.VTK;

namespace VtkMvvm.Features.BrushPainter;

/// <summary>
///     Converts a brush <see cref="vtkPolyData" /> into a flat list of “ready-to-use” linear
///     voxel offsets that match the X/Y/Z strides of a target <see cref="vtkImageData" />.
/// </summary>
public sealed class BrushLinearOffsetCache
{
    private const byte ThrByte = 128; // should between stencil inside and outside value
    private readonly List<int> _linearOffsets = new(256);

    // ────────────────  VTK pipeline nodes  ──────────────────────────────────────
    private readonly vtkTransform _mmToIjk = new();
    private readonly vtkPolyDataToImageStencil _poly2Stencil = new();
    private readonly vtkImageStencilToImage _stencil2Mask = new();
    private readonly vtkTransformPolyDataFilter _transform = new();

    private readonly List<(int dx, int dy, int dz)> _tupleOffsets = new(256);

    // ────────────────  cache bookkeeping  ──────────────────────────────────────
    private uint _cachedMTime;
    private (int row, int slice) _cachedStrides = (-1, -1);

    // ────────────────  ctor & pipeline glue  ───────────────────────────────────
    public BrushLinearOffsetCache()
    {
        _transform.SetTransform(_mmToIjk);

        _poly2Stencil.SetOutputSpacing(1, 1, 1); // work in IJK space

        _stencil2Mask.SetInsideValue(255);
        _stencil2Mask.SetOutsideValue(0);
        _stencil2Mask.SetOutputScalarTypeToUnsignedChar();

        _poly2Stencil.SetInputConnection(_transform.GetOutputPort());
        _stencil2Mask.SetInputConnection(_poly2Stencil.GetOutputPort());
    }

    // ────────────────  public API  ─────────────────────────────────────────────
    /// <summary>Inject the brush geometry (a vtkPolyData in *world* coordinates).</summary>
    public void SetBrushGeometry(vtkAlgorithmOutput polyWorldPort)
    {
        _transform.SetInputConnection(polyWorldPort);
        _transform.Update();
        TightenMaskExtent();
    }

    /// <summary>Tell the cache which label-map we’ll paint onto.</summary>
    public void BindLabelMapInfo(vtkImageData labelMap)
    {
        double[]? sp = labelMap.GetSpacing();
        int[]? dim = labelMap.GetDimensions();

        _mmToIjk.Identity();
        _mmToIjk.Scale(1 / sp[0], 1 / sp[1], 1 / sp[2]);
        _mmToIjk.Modified();

        _cachedStrides = (row: dim[0], slice: dim[0] * dim[1]);
    }

    /// <summary>
    ///     Returns linear offsets whose voxel are inside the brush poly stencil.
    ///     Rebuilds the cache only when the brush geometry, label-map stride changes.
    /// </summary>
    public ReadOnlySpan<int> GetLinearOffsets()
    {
        if (_cachedStrides.row < 0)
            throw new InvalidOperationException("Call BindLabelMapInfo first.");

        // Run the pipeline (cheap if nothing upstream changed) so we can read the fresh MTime.
        _stencil2Mask.Update();
        uint newMTime = _stencil2Mask.GetOutput().GetPipelineMTime();

        bool needRebuild =
            newMTime != _cachedMTime ||
            _cachedStrides == (-1, -1); // first use (row/slice set later)

        if (needRebuild) BuildCaches(newMTime);

        return CollectionsMarshal.AsSpan(_linearOffsets);
    }

    // ────────────────  helpers  ────────────────────────────────────────────────
    private void TightenMaskExtent()
    {
        double[]? bounds = _transform.GetOutput().GetBounds();
        _poly2Stencil.SetOutputWholeExtent(
            (int)Math.Floor(bounds[0]) - 1, (int)Math.Ceiling(bounds[1]) + 1,
            (int)Math.Floor(bounds[2]) - 1, (int)Math.Ceiling(bounds[3]) + 1,
            (int)Math.Floor(bounds[4]) - 1, (int)Math.Ceiling(bounds[5]) + 1);
    }

    private void BuildCaches(uint newMTime)
    {
        vtkImageData mask = _stencil2Mask.GetOutput();
        FillTupleOffsets(mask);

        // turn (dx,dy,dz) → linear index with the *current* strides
        _linearOffsets.Clear();
        foreach ((int dx, int dy, int dz) in _tupleOffsets)
            _linearOffsets.Add(dz * _cachedStrides.slice + dy * _cachedStrides.row + dx);

        // book-keeping
        _cachedMTime = newMTime;
    }

    private void FillTupleOffsets(vtkImageData mask)
    {
        _tupleOffsets.Clear();

        int[] ext = mask.GetExtent();
        for (int z = ext[4]; z <= ext[5]; ++z)
        for (int y = ext[2]; y <= ext[3]; ++y)
        for (int x = ext[0]; x <= ext[1]; ++x)
            if (mask.GetScalarComponentAsDouble(x, y, z, 0) >= ThrByte)
                _tupleOffsets.Add((x, y, z));
    }
}