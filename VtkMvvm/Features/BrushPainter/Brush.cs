using System.Diagnostics;
using Kitware.VTK;

namespace VtkMvvm.Features.BrushPainter;

// ──────────────────────────────────────────────────────────────────────────────
//  V O X E L   B R U S H   M O D E L S
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A brush described in *voxel index space* (IJK).  When the brush is
/// centred on an **integer** voxel coordinate, every offset in
/// <see cref="Offsets"/> is coloured.
///
/// Think of <c>Offsets</c> as a stamp carved out of Lego bricks – the stamp
/// pattern never changes once the object is constructed, so one instance
/// can be reused for any number of paint operations.
/// </summary>
public abstract record VoxelBrush(
    IReadOnlyList<(int dx, int dy, int dz)> Offsets);

// ──────────────────────────────────────────────────────────────────────────────
//  C Y L I N D E R   ( 2-D   C I R C L E )   B R U S H
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A “cylindrical” brush (circle on a single slice, 1-slice thick) generated
/// once for a *specific voxel spacing*.  Physical size is specified in
/// millimetres, but the stored offsets are **in voxels**, so the painter
/// can work with nothing but integer adds inside the hot loop.
/// </summary>
public sealed record VoxelCylinderBrush : VoxelBrush
{
    public enum Axis
    {
        X,
        Y,
        Z
    }

    private VoxelCylinderBrush(IReadOnlyList<(int, int, int)> offsets)
        : base(offsets)
    {
    }

    /// <param name="spacing">
    /// (sx, sy, sz) voxel dimensions in millimetres.  Determines how many
    /// voxels correspond to the requested physical size.
    /// </param>
    /// <param name="diameterMm">Desired circle diameter in mm.</param>
    /// <param name="heightMm">
    /// Height in mm along the slice normal.  Normally the same as
    /// <c>sz</c> for “one slice thick”, but a thicker stamp (e.g. for
    /// oblique viewing) is possible.
    /// </param>
    /// <param name="axis">Axis that the cylinder height will be aligned</param>
    /// <param name="resolution">
    /// Facet count for the intermediate VTK cylinder mesh; 32 is visually
    /// smooth while still cheap to voxelise.
    /// </param>
    public static VoxelCylinderBrush Create(
        (double sx, double sy, double sz) spacing,
        double diameterMm,
        double heightMm,
        Axis axis = Axis.Z,
        int resolution = 32)
    {
        // ── 1. Build a cylinder in *mm* (VTK default = axis || ẑ) ──────────
        var cyl = vtkCylinderSource.New();
        cyl.SetRadius(diameterMm / 2.0); // mm
        cyl.SetHeight(heightMm); // mm
        cyl.SetResolution(resolution);
        cyl.CappingOn(); // nicer voxelisation
        cyl.Update();

        // ── 2. Orientation transform ──────────────────────────────────────
        var orient = vtkTransform.New();
        orient.PreMultiply();

        // a) Scale mm → voxel index (non-uniform)
        var (sx, sy, sz) = spacing;
        orient.Scale(1.0 / sx, 1.0 / sy, 1.0 / sz); // number of the voxels drawn increased for thinner voxel 

        // b) Rotate default y-axis to requested X, Y, or Z
        switch (axis)
        {
            case Axis.X: orient.RotateZ(-90); break; // y -> x
            case Axis.Y: /*nothing*/ break; // already y
            case Axis.Z: orient.RotateX(90); break; // y -> z
        }

        // ── 3. Apply transform & voxelise at *unit* spacing ───────────────
        var cylVox = vtkTransformPolyDataFilter.New();
        cylVox.SetTransform(orient);
        cylVox.SetInputConnection(cyl.GetOutputPort());
        cylVox.Update();

        // debug...
        var bounds = cylVox.GetOutput().GetBounds();
        Debug.WriteLine($"bounds: {bounds[0]} {bounds[1]} {bounds[2]} {bounds[3]} {bounds[4]} {bounds[5]}");

        var toStencil = vtkPolyDataToImageStencil.New();
        toStencil.SetInputConnection(cylVox.GetOutputPort());
        toStencil.SetOutputSpacing(1, 1, 1); // we are **now** in IJK

        // tight bbox with a 1-voxel pad
        var b = cylVox.GetOutput().GetBounds();
        toStencil.SetOutputWholeExtent(
            (int)Math.Floor(b[0]) - 1, (int)Math.Ceiling(b[1]) + 1,
            (int)Math.Floor(b[2]) - 1, (int)Math.Ceiling(b[3]) + 1,
            (int)Math.Floor(b[4]) - 1, (int)Math.Ceiling(b[5]) + 1);
        toStencil.Update();

        var maskImg = vtkImageStencilToImage.New();
        maskImg.SetInputConnection(toStencil.GetOutputPort());
        maskImg.SetInsideValue(1);
        maskImg.SetOutsideValue(0);
        maskImg.SetOutputScalarTypeToUnsignedChar();
        maskImg.Update();

        // ── 4. Harvest offsets where voxel == 1 ────────────────────────────
        var mask = maskImg.GetOutput();
        var ext = mask.GetExtent();
        var off = new List<(int, int, int)>();

        for (int z = ext[4]; z <= ext[5]; ++z)
        for (int y = ext[2]; y <= ext[3]; ++y)
        for (int x = ext[0]; x <= ext[1]; ++x)
            if (mask.GetScalarComponentAsDouble(x, y, z, 0) > 0.5)
                off.Add((x, y, z));

        return new VoxelCylinderBrush(off);
    }
}