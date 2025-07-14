using Kitware.VTK;

namespace VtkMvvm.Features.InteractorBehavior;

/// <summary>
/// Combination of keyboard keys that must be held down for the callback to fire.
/// The "None" requires strictly NO key is pressing.
/// </summary>
[Flags]
public enum KeyModifier
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
}

internal static class KeyMaskExtensions
{
    public static bool IsSatisfied(this KeyModifier key, vtkRenderWindowInteractor iren)
    {
        bool alt = iren.GetAltKey() != 0;
        bool ctrl = iren.GetControlKey() != 0;
        bool shift = iren.GetShiftKey() != 0;
        
        if (key == KeyModifier.None)
            return !alt && !ctrl && !shift; // ← strict zero

        if (key.HasFlag(KeyModifier.Alt) && !alt) return false;
        if (key.HasFlag(KeyModifier.Ctrl) && !ctrl) return false;
        if (key.HasFlag(KeyModifier.Shift) && !shift) return false;
        return true;
    }
}