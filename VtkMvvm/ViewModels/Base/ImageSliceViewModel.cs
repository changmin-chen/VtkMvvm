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
    private readonly vtkImageMapToColors _colorMap = vtkImageMapToColors.New();
    private readonly IColorMappingStrategy _colorStrategy;
    protected vtkImageMapToColors ColorMap => _colorMap;
    protected ImageSliceViewModel(ColoredImagePipeline pipe)
    {
        _colorStrategy = pipe.IsRgba ? new LabelMapColorMapping(pipe) : new WindowLevelColorMapping(pipe);
        _colorStrategy.Apply(_colorMap);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _colorMap.Dispose();
        base.Dispose(disposing);
    }
    
    public double WindowLevel
    {
        get => (_colorStrategy as WindowLevelColorMapping)?.Level ?? 0;
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
        get => (_colorStrategy as WindowLevelColorMapping)?.Window ?? 0;
        set
        {
            if (_colorStrategy is not WindowLevelColorMapping wlv) return;
            wlv.Window = value;
            OnPropertyChanged();
            OnModified();
        }
    }
    
    // ── slice orientation info ─────────────────────
    public Vector3 PlaneNormal { get; protected set; }
    public Vector3 PlaneAxisU { get; protected set; }
    public Vector3 PlaneAxisV { get; protected set; }
    public Double3 PlaneOrigin { get; protected set; }

    // ── slice index (still abstract: each VM applies it differently)
    private int _sliceIndex = int.MinValue;

    public virtual int SliceIndex
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