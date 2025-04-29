namespace VtkMvvm.Models;

/// <summary>
/// Helper enum to indicate the orientation of the slice.
/// Axial orientation slice with fixed z; Coronal orientation slice with fixed y; Sagittal orientation slice with fixed x.
/// </summary>
public enum SliceOrientation
{
    Axial,
    Coronal,
    Sagittal
}