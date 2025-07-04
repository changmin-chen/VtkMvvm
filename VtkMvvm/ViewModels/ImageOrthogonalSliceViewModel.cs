﻿using System.Numerics;
using Kitware.VTK;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels.Base;

namespace VtkMvvm.ViewModels;

/// <summary>
/// Leverage VTK image actor instead of reslicing the image. Simpler and suitable for orthogonal slices.
/// </summary>
public sealed class ImageOrthogonalSliceViewModel : VtkElementViewModel, ISlicePlaneInfo
{
    private readonly vtkImageMapToColors _cmap = vtkImageMapToColors.New();
    private readonly double[] _origin;
    private readonly double[] _spacing;
    private int _sliceIndex = int.MinValue;
    private double _windowLevel;
    private double _windowWidth;

    public ImageOrthogonalSliceViewModel(SliceOrientation orientation, ColoredImagePipeline pipeline)
    {
        Orientation = orientation;
        (PlaneAxisU, PlaneAxisV) = GetPlaneAxes(orientation);
        PlaneNormal = Vector3.Normalize(Vector3.Cross(PlaneAxisU, PlaneAxisV));

        vtkImageData image = pipeline.Image;
        _origin = image.GetOrigin();
        _spacing = image.GetSpacing();

        vtkImageActor actor = vtkImageActor.New();
        Actor = actor;
        ImageModel = ImageModel.Create(image);
        pipeline.Connect(_cmap, actor);

        // SetSliceIndex here is necessary.
        // This not only affects which slice it initially displayed, but also affects how the View recognizes the slicing orientation
        SliceIndex = 0;
    }

    private static (Vector3 uDir, Vector3 vDir) GetPlaneAxes(SliceOrientation orientation) => orientation switch
    {
        SliceOrientation.Axial => (Vector3.UnitX, Vector3.UnitZ),
        SliceOrientation.Coronal => (Vector3.UnitY, Vector3.UnitX),
        SliceOrientation.Sagittal => (Vector3.UnitZ, Vector3.UnitY),
        _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null)
    };

    public SliceOrientation Orientation { get; }
    public ImageModel ImageModel { get; }
    public override vtkImageActor Actor { get; }

    // --------- sliced plane information -----------------------------
    public Vector3 PlaneNormal { get; }
    public Vector3 PlaneAxisU { get; }
    public Vector3 PlaneAxisV { get; }
    public Double3 PlaneOrigin { get; private set; }

    public int SliceIndex
    {
        get => _sliceIndex;
        set
        {
            if (!SetField(ref _sliceIndex, value)) return;
            SetSliceIndex(value);
            OnModified();
        }
    }

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

    public double Opacity
    {
        get => Actor.GetOpacity();
        set
        {
            if (Math.Abs(Actor.GetOpacity() - value) < 1e-3) return;
            Actor.SetOpacity(value);
            Actor.Modified();
            OnPropertyChanged();
            OnModified();
        }
    }


    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cmap.Dispose();
        }
        base.Dispose(disposing);
    }

    private void SetSliceIndex(int sliceIndex)
    {
        int[] dims = ImageModel.Dimensions;

        // --- tell the actor which slice to draw ----------------------
        switch (Orientation)
        {
            case SliceOrientation.Axial:
                Actor.SetDisplayExtent(0, dims[0] - 1, 0, dims[1] - 1, sliceIndex, sliceIndex);
                break;
            case SliceOrientation.Coronal:
                Actor.SetDisplayExtent(0, dims[0] - 1, sliceIndex, sliceIndex, 0, dims[2] - 1);
                break;
            case SliceOrientation.Sagittal:
                Actor.SetDisplayExtent(sliceIndex, sliceIndex, 0, dims[1] - 1, 0, dims[2] - 1);
                break;
        }

        // --- compute world-space origin of that plane ----------------
        double ox = _origin[0];
        double oy = _origin[1];
        double oz = _origin[2];
        switch (Orientation)
        {
            case SliceOrientation.Axial: oz += sliceIndex * _spacing[2]; break;
            case SliceOrientation.Coronal: oy += sliceIndex * _spacing[1]; break;
            case SliceOrientation.Sagittal: ox += sliceIndex * _spacing[0]; break;
        }
        PlaneOrigin = new Double3(ox, oy, oz);
        OnPropertyChanged(nameof(PlaneOrigin));

        Actor.Modified();
    }

    private void SetWindowBand(double level, double width)
    {
        double low = level - width * 0.5;
        double high = level + width * 0.5;

        vtkScalarsToColors? lut = _cmap.GetLookupTable();
        lut.SetRange(low, high);
        lut.Build();
        _cmap.SetLookupTable(lut);

        _cmap.Modified();
        Actor.Modified();
    }
}