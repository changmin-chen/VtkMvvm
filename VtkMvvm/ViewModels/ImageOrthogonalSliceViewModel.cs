using System.Numerics;
using Kitware.VTK;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels.Base;

namespace VtkMvvm.ViewModels;

/// <summary>
/// Leverage VTK image actor instead of reslicing the image. Simpler and suitable for orthogonal slices.
/// </summary>
public sealed class ImageOrthogonalSliceViewModel : ImageSliceViewModel
{
    private readonly double[] _origin;
    private readonly double[] _spacing;

    public ImageOrthogonalSliceViewModel(SliceOrientation orientation, ColoredImagePipeline pipe) : base(pipe)
    {
        Orientation = orientation;
        (PlaneAxisU, PlaneAxisV) = GetPlaneAxes(orientation);
        PlaneNormal = Vector3.Normalize(Vector3.Cross(PlaneAxisU, PlaneAxisV));

        vtkImageData image = pipe.Image;
        _origin = image.GetOrigin();
        _spacing = image.GetSpacing();

        vtkImageActor actor = vtkImageActor.New();
        Actor = actor;
        ImageModel = ImageModel.Create(image);
        
        // VTK plumping
        ColorMap.SetInput(pipe.Image);
        actor.SetInput(ColorMap.GetOutput());
        
        // SetSliceIndex here is necessary.
        // This not only affects which slice it initially displayed, but also affects how the View recognizes the slicing orientation
        SliceIndex = 0;
    }

    private static (Vector3 uDir, Vector3 vDir) GetPlaneAxes(SliceOrientation orientation) => orientation switch
    {
        SliceOrientation.Axial => (Vector3.UnitX, Vector3.UnitY),
        SliceOrientation.Coronal => (Vector3.UnitX, Vector3.UnitZ),
        SliceOrientation.Sagittal => (Vector3.UnitY, Vector3.UnitZ),
        _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null)
    };

    public SliceOrientation Orientation { get; }
    public ImageModel ImageModel { get; }
    public override vtkImageActor Actor { get; }


    

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
    

    protected override void OnSliceIndexChanged(int idx)
    {
        int[] dims = ImageModel.Dimensions;

        // --- tell the actor which slice to draw ----------------------
        switch (Orientation)
        {
            case SliceOrientation.Axial:
                idx = Math.Clamp(idx, 0, dims[2] - 1);
                Actor.SetDisplayExtent(0, dims[0] - 1, 0, dims[1] - 1, idx, idx);
                break;
            case SliceOrientation.Coronal:
                idx = Math.Clamp(idx, 0, dims[1] - 1);
                Actor.SetDisplayExtent(0, dims[0] - 1, idx, idx, 0, dims[2] - 1);
                break;
            case SliceOrientation.Sagittal:
                idx = Math.Clamp(idx, 0, dims[0] - 1);
                Actor.SetDisplayExtent(idx, idx, 0, dims[1] - 1, 0, dims[2] - 1);
                break;
        }

        // --- compute world-space origin of that plane ----------------
        double ox = _origin[0];
        double oy = _origin[1];
        double oz = _origin[2];
        switch (Orientation)
        {
            case SliceOrientation.Axial: oz += idx * _spacing[2]; break;
            case SliceOrientation.Coronal: oy += idx * _spacing[1]; break;
            case SliceOrientation.Sagittal: ox += idx * _spacing[0]; break;
        }
        PlaneOrigin = new Double3(ox, oy, oz);
        OnPropertyChanged(nameof(PlaneOrigin));

        Actor.Modified();
    }
}