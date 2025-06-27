using System.Numerics;

namespace VtkMvvm.Models;

public readonly record struct Double3(double X, double Y, double Z)
{
    public static Double3 Zero => new(0, 0, 0);
    public static Double3 One => new(1, 1, 1);
    public static Double3 UnitX => new(1, 0, 0);
    public static Double3 UnitY => new(0, 1, 0);
    public static Double3 UnitZ => new(0, 0, 1);

    // Instance properties
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public double LengthSquared => X * X + Y * Y + Z * Z;

    // Normalization
    public Double3 Normalized()
    {
        var length = Length;
        if (length < double.Epsilon)
            return Zero;
        return new Double3(X / length, Y / length, Z / length);
    }
}

public static class Double3Extensions
{
    public static double[] ToArray(this Double3 d) => [d.X, d.Y, d.Z];
    
    /// <summary>
    /// Downcast to float precision. 
    /// </summary>
    public static Vector3 ToVector3(this Double3 d) => new((float)d.X, (float)d.Y, (float)d.Z);
}