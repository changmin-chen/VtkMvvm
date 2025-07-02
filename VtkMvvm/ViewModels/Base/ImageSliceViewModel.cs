using System.Numerics;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels.Base;

/// <summary>
/// Slice orientation info of the sliced 2D image plane in the 3D space.
/// Useful for camera positioning.
/// </summary>
public abstract class ImageSliceViewModel : VtkElementViewModel
{
    // ── slice orientation info ─────────────────────
    public Vector3 PlaneNormal { get; protected set; }
    public Vector3 PlaneAxisU { get; protected set; }
    public Vector3 PlaneAxisV { get; protected set; }
    public Double3 PlaneOrigin { get; protected set; }

    // ── slice index (still abstract: each VM applies it differently)
    private int _sliceIndex = int.MinValue;

    public int SliceIndex
    {
        get => _sliceIndex;
        set
        {
            if (!SetField(ref _sliceIndex, value)) return;
            ApplySliceIndex(value); // concrete hook
            OnModified();
        }
    }

    protected abstract void ApplySliceIndex(int idx);
}
