using System.Numerics;

namespace VtkMvvm.Models;

public readonly record struct Double3(double X, double Y, double Z)
{
    public static Double3 Zero => new(0, 0, 0);
    
    /// <summary>
    /// Downcast to float 
    /// </summary>
    public static explicit operator Vector3(Double3 d) => new((float)d.X, (float)d.Y, (float)d.Z);

}

public static class Double3Extensions
{
    public static double[] ToArray(this Double3 d) => [d.X, d.Y, d.Z];
}