using System.Numerics;
using Kitware.VTK;

namespace VtkMvvm.Extensions.Internal;

internal static class VtkTransformExtensions
{
    /// <summary>
    ///     Extensional helper for setting <see cref="vtkTransform" /> rotation directly through Quaternion.
    /// </summary>
    public static void RotateWithQuaternion(this vtkTransform vtkTransform, Quaternion q)
    {
        q.ToAxisAngle(out Vector3 axis, out float angle);
        vtkTransform.RotateWXYZ(angle, axis.X, axis.Y, axis.Z);
    }
}