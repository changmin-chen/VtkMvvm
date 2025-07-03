namespace VtkMvvm.Models;

/// <summary>
/// Axis-aligned bounding box (AABB) bounds value object.
/// </summary>
public readonly record struct Bounds(double XMin, double XMax, double YMin, double YMax, double ZMin, double ZMax)
{
    public Double3 Center => new((XMax + XMin) / 2, (YMax + YMin) / 2, (ZMax + ZMin) / 2);
    public double Width => Math.Abs(XMax - XMin);
    public double Height => Math.Abs(YMax - YMin);
    public double Depth => Math.Abs(ZMax - ZMin);

    public static Bounds FromArray(double[] bounds)
    {
        if (bounds.Length != 6) throw new ArgumentException("Bounds array must have 6 elements", nameof(bounds));
        return new(bounds[0], bounds[1], bounds[2], bounds[3], bounds[4], bounds[5]);
    }
    public static explicit operator Bounds(double[] bounds) => FromArray(bounds);
    
    public override string ToString() => $"[{XMin:F3}, {XMax:F3}, {YMin:F3}, {YMax:F3}, {ZMin:F3}, {ZMax:F3}]";
}