using System.Numerics;

namespace VtkMvvm.Extensions;

internal static class QuaternionExtensions
{
    /// <summary>
    ///     Converts a System.Numerics.Quaternion to its axis-angle representation.
    /// </summary>
    /// <param name="quaternion">The quaternion to convert.</param>
    /// <param name="axis">The output axis of rotation.</param>
    /// <param name="angle">The output angle of rotation in degrees.</param>
    public static void ToAxisAngle(this Quaternion quaternion, out Vector3 axis, out float angle)
    {
        // It's recommended to normalize the quaternion first.
        quaternion = Quaternion.Normalize(quaternion);

        // Calculate the angle.
        // The quaternion's W component is cos(angle / 2).
        angle = 2 * (float)Math.Acos(quaternion.W);

        // Calculate the axis.
        float s = (float)Math.Sqrt(1.0f - quaternion.W * quaternion.W);

        if (s < 0.001f)
        {
            // If s is close to zero, the axis is not well-defined.
            // This happens when the angle is close to 0 or 360 degrees.
            // In this case, we can default to any arbitrary axis, for example, the X-axis.
            axis = new Vector3(1.0f, 0.0f, 0.0f);
        }
        else
        {
            // The axis is the vector part of the quaternion divided by sin(angle / 2).
            axis = new Vector3(quaternion.X / s, quaternion.Y / s, quaternion.Z / s);
        }

        // Convert angle from radians to degrees.
        angle = angle * (180.0f / (float)Math.PI);
    }
}