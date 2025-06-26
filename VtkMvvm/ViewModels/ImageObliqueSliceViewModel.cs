using System.Numerics;
using Kitware.VTK;
using VtkMvvm.Extensions;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels;

public class ImageObliqueSliceViewModel : VtkElementViewModel
{
    private readonly vtkImageReslice _reslice = vtkImageReslice.New();
    private readonly vtkImageMapToColors _colorMap;
    private readonly vtkMatrix4x4 _axes = vtkMatrix4x4.New();
    private readonly double[] _imageCenter;

    /// <summary>
    ///     distance in mm between two adjacent native slices along the chosen normal. For isotropic volumes you can just pass
    ///     volume.GetSpacing()[0].
    /// </summary>
    private readonly double _nativeSpacing;

    private int _sliceIndex;

    public ImageObliqueSliceViewModel(
        Quaternion orientation,
        double nativeSpacing,
        ColoredImagePipeline pipe)
    {
        _nativeSpacing = nativeSpacing;
        _colorMap = pipe.ColorMap;

        ImageModel = ImageModel.Create(pipe.Image);
        _imageCenter = ImageModel.Center;

        // Configure reslice
        _reslice.SetInput(pipe.Image);
        _reslice.SetInterpolationModeToLinear();
        _reslice.AutoCropOutputOn(); // trims black borders
        _reslice.SetOutputDimensionality(2); // 2-D slice

        // Set Reslice axis
        SetOrientation(orientation); // fills _axes and calls Modified()

        // Connect pipeline manually: Resliced -> ColorMap -> Actor. The pipe.Connect() does not fit to this case
        vtkImageActor actor = pipe.Actor;
        _colorMap.SetInputConnection(_reslice.GetOutputPort());
        actor.SetInput(_colorMap.GetOutput());
        actor.Modified();

        // Init
        Actor = pipe.Actor;
        SetSliceIndex(0);
    }

    // ── Public surface identical to orthogonal VM ──
    public override vtkImageActor Actor { get; }

    public ImageModel ImageModel { get; }

    public int SliceIndex
    {
        get => _sliceIndex;
        set => SetSliceIndex(value);
    }


    // ── Orientation & scrolling helpers ──
    public void SetOrientation(Quaternion q)
    {
        q.ToAxisAngle(out Vector3 axis, out float angle);

        using vtkTransform? tf = vtkTransform.New();
        tf.Identity();
        tf.RotateWXYZ(angle, axis.X, axis.Y, axis.Z);
        vtkMatrix4x4 rot = tf.GetMatrix();
        for (int r = 0; r < 3; ++r)
        {
            for (int c = 0; c < 3; ++c)
            {
                _axes.SetElement(r, c, rot.GetElement(r, c));
            }
        }

        // translation handled in SetSliceIndex
        _axes.SetElement(3, 0, 0);
        _axes.SetElement(3, 1, 0);
        _axes.SetElement(3, 2, 0);
        _axes.SetElement(3, 3, 1);
        _reslice.SetResliceAxes(_axes);
        _reslice.Modified();
    }

    private void SetSliceIndex(int idx)
    {
        if (idx == _sliceIndex) return;
        _sliceIndex = idx;

        double nx = _axes.GetElement(0, 2);
        double ny = _axes.GetElement(1, 2);
        double nz = _axes.GetElement(2, 2);

        _axes.SetElement(0, 3, _imageCenter[0] + nx * idx * _nativeSpacing);
        _axes.SetElement(1, 3, _imageCenter[1] + ny * idx * _nativeSpacing);
        _axes.SetElement(2, 3, _imageCenter[2] + nz * idx * _nativeSpacing);

        _reslice.SetResliceAxes(_axes);
        _reslice.Modified(); // forces recompute next Render()
        Actor.Modified();
        OnModified();
    }
}