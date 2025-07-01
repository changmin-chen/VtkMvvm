using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;

/// <summary>
/// Combination of keyboard keys that must be held down for the callback to fire.
/// The "None" requires strickly NO key is pressing.
/// </summary>
[Flags]
public enum KeyMask
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
}

internal static class KeyMaskExtensions
{
    public static bool IsSatisfied(this KeyMask mask, vtkRenderWindowInteractor iren)
    {
        bool alt = iren.GetAltKey() != 0;
        bool ctrl = iren.GetControlKey() != 0;
        bool shift = iren.GetShiftKey() != 0;
        
        if (mask == KeyMask.None)
            return !alt && !ctrl && !shift; // ← strict zero

        if (mask.HasFlag(KeyMask.Alt) && !alt) return false;
        if (mask.HasFlag(KeyMask.Ctrl) && !ctrl) return false;
        if (mask.HasFlag(KeyMask.Shift) && !shift) return false;
        return true;
    }
}