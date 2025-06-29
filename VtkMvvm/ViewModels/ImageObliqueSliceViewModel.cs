using System.Numerics;
using Kitware.VTK;
using VtkMvvm.Extensions.Internal;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels;

public class ImageObliqueSliceViewModel : VtkElementViewModel
{
    private readonly ColoredImagePipeline _pipeline;
    private readonly vtkImageReslice _reslice = vtkImageReslice.New();
    private readonly vtkMatrix4x4 _axes = vtkMatrix4x4.New();
    private readonly double[] _imgCentre;
    private readonly double[] _imgBounds;

    /// <summary>
    ///     distance in mm between two adjacent native slices along the chosen normal. For isotropic volumes you can just pass
    ///     volume.GetSpacing()[0].
    /// </summary>
    private double _step; // Δ (mm per SliceIndex)

    private int _minSliceIdx; // slider bound (-)
    private int _maxSliceIdx; // slider bound (+)
    private int _sliceIndex = int.MinValue;
    private Quaternion _sliceOrientation = Quaternion.Identity;

    public ImageObliqueSliceViewModel(
        Quaternion orientation,
        ColoredImagePipeline pipeline)
    {
        _pipeline = pipeline;

        vtkImageData image = pipeline.Image;
        ImageModel = ImageModel.Create(pipeline.Image);
        _imgCentre = image.GetCenter();
        _imgBounds = image.GetBounds();

        // Configure reslice
        _reslice.SetInput(image);
        _reslice.SetInterpolationModeToLinear();
        _reslice.AutoCropOutputOn(); // trims black borders
        _reslice.SetOutputDimensionality(2); // 2-D slice
        _reslice.SetBackgroundLevel(image.GetScalarRange()[0]); // fill the background with min scalar value

        // Connect pipeline: Reslice → ColorMap → Actor. 
        vtkImageActor actor = vtkImageActor.New();
        Actor = actor;
        pipeline.ConnectWithReslice(actor, _reslice);

        // orientation also sets step size and slider limits
        SliceOrientation = orientation;

        // initialise at centre slice.  
        SliceIndex = 0;
    }

    // ── Public surface identical to orthogonal VM ──
    public override vtkImageActor Actor { get; }
    public ImageModel ImageModel { get; }

    public Bounds GetSliceBounds() 
    {
        _reslice.Update();
        vtkImageData img = _reslice.GetOutput();
        return Bounds.FromArray(img.GetBounds());
    }


    #region Bindable Properties

    public int SliceIndex
    {
        get => _sliceIndex;
        set
        {
            value = Math.Clamp(value, _minSliceIdx, _maxSliceIdx);
            if (SetField(ref _sliceIndex, value))
            {
                SetSliceIndex(value);
                OnModified();
            }
        }
    }

    public Quaternion SliceOrientation
    {
        get => _sliceOrientation;
        set
        {
            if (SetField(ref _sliceOrientation, value))
            {
                SetOrientation(value);
                OnModified();
            }
        }
    }

    public int MinSliceIndex => _minSliceIdx;
    public int MaxSliceIndex => _maxSliceIdx;

    /// <summary>
    /// vtkImageReslice expects a world → slice matrix in ResliceAxes. So we extract from row instead of column, which is mathematically inversed.
    /// </summary>
    public Double3 PlaneAxisU // slice X-axis in world coords
        => new(_axes.GetElement(0, 0),
            _axes.GetElement(0, 1),
            _axes.GetElement(0, 2));

    public Double3 PlaneAxisV // slice Y-axis in world coords
        => new(_axes.GetElement(1, 0),
            _axes.GetElement(1, 1),
            _axes.GetElement(1, 2));

    public Double3 PlaneNormal // slice Z-axis (normal) in world coords
        => new(_axes.GetElement(2, 0),
            _axes.GetElement(2, 1),
            _axes.GetElement(2, 2));

    #endregion


    /// <summary>
    /// Update the orientation of the reslicing quaternion and recompute the allowed slice index boundary.
    /// </summary>
    private void SetOrientation(Quaternion q)
    {
        // 1) apply the quaternion to the reslicing grid
        using var tf = vtkTransform.New();
        tf.Identity();
        tf.RotateWithQuaternion(q);

        vtkMatrix4x4 rot = tf.GetMatrix();
        for (int r = 0; r < 3; ++r)
        {
            for (int c = 0; c < 3; ++c)
            {
                _axes.SetElement(r, c, rot.GetElement(r, c));
            }
        }

        _axes.SetElement(3, 0, 0);
        _axes.SetElement(3, 1, 0);
        _axes.SetElement(3, 2, 0);
        _axes.SetElement(3, 3, 1);
        _reslice.SetResliceAxes(_axes);
        _reslice.Modified();

        // 2) derive step Δ (mm per index) ------------------------------
        double[] sp = ImageModel.Spacing; // (sx, sy, sz)
        double nx = Math.Abs(_axes.GetElement(0, 2));
        double ny = Math.Abs(_axes.GetElement(1, 2));
        double nz = Math.Abs(_axes.GetElement(2, 2));
        _step = nx * sp[0] + ny * sp[1] + nz * sp[2];

        // 3) support-function distance to box --------------------------
        double hx = 0.5 * (_imgBounds[1] - _imgBounds[0]);
        double hy = 0.5 * (_imgBounds[3] - _imgBounds[2]);
        double hz = 0.5 * (_imgBounds[5] - _imgBounds[4]);
        double maxDist = nx * hx + ny * hy + nz * hz;

        int maxIdx = (int)Math.Floor(maxDist / _step);
        _minSliceIdx = -maxIdx;
        _maxSliceIdx = maxIdx;

        // notify bindings
        OnPropertyChanged(nameof(MinSliceIndex));
        OnPropertyChanged(nameof(MaxSliceIndex));

        // keep the current slice index within the new range
        SliceIndex = Math.Clamp(_sliceIndex, _minSliceIdx, _maxSliceIdx);
    }

    /// <summary>
    /// We define 'sliceOrigin(i) = centre + n · (i · Δ)'
    /// centre: the dataset centre (GetCenter) we chose as index 0
    /// n: unit normal of the reslice plane (third column of the axes matrix)
    /// Δ: physical step per index
    /// </summary>
    private void SetSliceIndex(int idx)
    {
        double nx = _axes.GetElement(0, 2);
        double ny = _axes.GetElement(1, 2);
        double nz = _axes.GetElement(2, 2);

        _axes.SetElement(0, 3, _imgCentre[0] + nx * idx * _step);
        _axes.SetElement(1, 3, _imgCentre[1] + ny * idx * _step);
        _axes.SetElement(2, 3, _imgCentre[2] + nz * idx * _step);

        _reslice.SetResliceAxes(_axes);
        _reslice.Modified();
        Actor.Modified();
    }
}