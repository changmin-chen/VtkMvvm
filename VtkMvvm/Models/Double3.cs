using System.Numerics;

namespace VtkMvvm.Models;

public readonly record struct Double3(double X, double Y, double Z)
{
    // Static properties
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

    // Arithmetic operators
    public static Double3 operator +(Double3 a, Double3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Double3 operator -(Double3 a, Double3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Double3 operator -(Double3 a) => new(-a.X, -a.Y, -a.Z);

    public static Double3 operator *(Double3 a, double scalar) => new(a.X * scalar, a.Y * scalar, a.Z * scalar);

    public static Double3 operator *(double scalar, Double3 a) => new(a.X * scalar, a.Y * scalar, a.Z * scalar);

    public static Double3 operator /(Double3 a, double scalar) => new(a.X / scalar, a.Y / scalar, a.Z / scalar);

    // Vector operations
    public static double Dot(Double3 a, Double3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Double3 Cross(Double3 a, Double3 b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X
    );

    public static double Distance(Double3 a, Double3 b) => (a - b).Length;

    public static double DistanceSquared(Double3 a, Double3 b) => (a - b).LengthSquared;

    // Interpolation
    public static Double3 Lerp(Double3 a, Double3 b, double t) => a + (b - a) * t;

    public static Double3 Slerp(Double3 a, Double3 b, double t)
    {
        var dot = Dot(a.Normalized(), b.Normalized());
        dot = Math.Max(-1.0, Math.Min(1.0, dot)); // Clamp to prevent numerical errors

        var theta = Math.Acos(dot) * t;
        var relativeVec = (b - a * dot).Normalized();

        return (a * Math.Cos(theta)) + (relativeVec * Math.Sin(theta));
    }

    // Utility methods
    public static Double3 Min(Double3 a, Double3 b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));

    public static Double3 Max(Double3 a, Double3 b) => new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));

    public static Double3 Abs(Double3 a) => new(Math.Abs(a.X), Math.Abs(a.Y), Math.Abs(a.Z));

    public static Double3 Clamp(Double3 value, Double3 min, Double3 max) => new(
        Math.Max(min.X, Math.Min(max.X, value.X)),
        Math.Max(min.Y, Math.Min(max.Y, value.Y)),
        Math.Max(min.Z, Math.Min(max.Z, value.Z))
    );

    // Reflection and projection
    public static Double3 Reflect(Double3 vector, Double3 normal) => vector - 2 * Dot(vector, normal) * normal;

    public static Double3 Project(Double3 vector, Double3 onto) => onto * (Dot(vector, onto) / Dot(onto, onto));

    // Angle between vectors (in radians)
    public static double Angle(Double3 a, Double3 b)
    {
        var dot = Dot(a.Normalized(), b.Normalized());
        return Math.Acos(Math.Max(-1.0, Math.Min(1.0, dot)));
    }

    // Component-wise multiplication
    public static Double3 Scale(Double3 a, Double3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

    // String representation
    public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";

    public string ToString(string format) => $"({X.ToString(format)}, {Y.ToString(format)}, {Z.ToString(format)})";
}

public static class Double3Extensions
{
    public static double[] ToArray(this Double3 d) => [d.X, d.Y, d.Z];

    /// <summary>
    /// Downcast to float precision. 
    /// </summary>
    public static Vector3 ToVector3(this Double3 d) => new((float)d.X, (float)d.Y, (float)d.Z);
}