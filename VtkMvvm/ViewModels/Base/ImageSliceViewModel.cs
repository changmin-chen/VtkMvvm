using System.Numerics;
using Kitware.VTK;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels.Components;

namespace VtkMvvm.ViewModels.Base;

/// <summary>
/// ViewModel base that cuts an 2D image slice from a 3D volume.
/// Brings orientation info of the sliced plane, which is necessary for camera positioning.
/// </summary>
public abstract class ImageSliceViewModel : VtkElementViewModel
{
    // ── color map ─────────────────────
    private readonly ColoredImagePipeline _pipeLine;  // holds private field to prevent GC
    private readonly IColorMappingStrategy _colorStrategy;
    
    /// <summary>
    /// Concrete actor override for displaying image slice in the scene
    /// </summary>
    public override vtkImageActor Actor { get; } = vtkImageActor.New();
    
    /// <summary>
    /// Maps sliced image to the colors. Its output should be connected to the actor
    /// </summary>
    protected vtkImageMapToColors ColorMap { get; } = vtkImageMapToColors.New();
    
    
    protected ImageSliceViewModel(ColoredImagePipeline pipeLine)
    {
        _pipeLine = pipeLine;
        _colorStrategy = pipeLine.IsRgba ? new LabelMapColorMapping(pipeLine) : new WindowLevelColorMapping(pipeLine);
        _colorStrategy.ApplyTo(ColorMap);
    }

    protected override void Dispose(bool disposing)
    {
        if (IsDisposed) return;
        
        if (disposing)
        {
            Actor.SetInput(null);
            ColorMap.RemoveAllObservers();
            ColorMap.Dispose();
            if (_colorStrategy is IDisposable d) d.Dispose();
        }
        
        base.Dispose(disposing);
    }
    
    public double WindowLevel
    {
        get => (_colorStrategy as WindowLevelColorMapping)?.Level ?? double.NaN;
        set
        {
            if (_colorStrategy is not WindowLevelColorMapping wlv) return;
            wlv.Level = value;
            OnPropertyChanged();
            OnModified();
        }
    }
    
    public double WindowWidth
    {
        get => (_colorStrategy as WindowLevelColorMapping)?.Window ?? double.NaN;
        set
        {
            if (_colorStrategy is not WindowLevelColorMapping wlv) return;
            wlv.Window = value;
            OnPropertyChanged();
            OnModified();
        }
    }
    
    // ── slice orientation info ─────────────────────
    
    /// <summary>
    /// Normal direction of the image slice
    /// </summary>
    public Vector3 PlaneNormal { get; protected set; }
    
    /// <summary>
    /// First axis direction of the image slice
    /// </summary>
    public Vector3 PlaneAxisU { get; protected set; }
    
    /// <summary>
    /// Second axis direction of the image slice
    /// </summary>
    public Vector3 PlaneAxisV { get; protected set; }
    
    /// <summary>
    /// World coordinate of the current image slice origin
    /// </summary>
    public Double3 PlaneOrigin { get; protected set; }

    // ── slice index (still abstract: each VM applies it differently) ─────────────────────
    private int _sliceIndex = int.MinValue;  // in ctor, ensure "SliceIndex = 0" can trigger OnSliceIndexChanged

    /// <summary>
    /// Index of the slice to display. The setter raises OnSliceIndexChanged
    /// </summary>
    public int SliceIndex
    {
        get => _sliceIndex;
        set
        {
            if (!SetField(ref _sliceIndex, value)) return;
            OnSliceIndexChanged(value); // concrete hook
            OnModified();
        }
    }

    protected abstract void OnSliceIndexChanged(int idx);
}