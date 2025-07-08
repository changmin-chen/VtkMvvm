using System.Numerics;

namespace VtkMvvm.Extensions;

internal static class QuaternionExtensions
{
    /// <summary>
    /// Compute the standard image grid unit axes that is rotated by the given quaternion. 
    /// </summary>
    public static (Vector3 uDir, Vector3 vDir, Vector3 nDir) GetTransformedUnitAxesDirections(this Quaternion q)
        => (Vector3.Transform(Vector3.UnitX, q), // +slice X (u)
            Vector3.Transform(Vector3.UnitY, q), // +slice Y (v / view-up)
            Vector3.Transform(Vector3.UnitZ, q)); // +slice Z (normal)
}