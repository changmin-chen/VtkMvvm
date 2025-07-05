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
    // color map ─────────────────────
    protected vtkImageMapToColors ColorMap { get; } = vtkImageMapToColors.New();
    protected override void Dispose(bool disposing)
    {
        if (disposing) ColorMap.Dispose();
        base.Dispose(disposing);
    }
    
    private double _windowLevel;
    private double _windowWidth;

    public double WindowLevel
    {
        get => _windowLevel;
        set
        {
            if (!SetField(ref _windowLevel, value)) return;
            SetWindowBand(value, WindowWidth);
            OnModified();
        }
    }

    public double WindowWidth
    {
        get => _windowWidth;
        set
        {
            if (!SetField(ref _windowWidth, value)) return;
            SetWindowBand(WindowLevel, value);
            OnModified();
        }
    }

    private void SetWindowBand(double level, double width)
    {
        double low = level - width * 0.5;
        double high = level + width * 0.5;

        vtkScalarsToColors? lut = ColorMap.GetLookupTable();
        lut.SetRange(low, high);
        lut.Build();
        ColorMap.SetLookupTable(lut);

        ColorMap.Modified();
        Actor.Modified();
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