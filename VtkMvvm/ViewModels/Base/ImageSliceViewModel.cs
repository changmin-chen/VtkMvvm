using System.Numerics;
using Kitware.VTK;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels.Base;

/// <summary>
/// ViewModel base that cuts an 2D image slice from a 3D volume.
/// Brings orientation info of the sliced plane, which is necessary for camera positioning.
/// </summary>
public abstract class ImageSliceViewModel : VtkElementViewModel
{
    protected vtkImageMapToColors ColorMap { get; } = vtkImageMapToColors.New();
    protected override void Dispose(bool disposing)
    {
        if (disposing) ColorMap.Dispose();
        base.Dispose(disposing);
    } 
    
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
