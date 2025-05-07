using System.Numerics;
using Kitware.VTK;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels;

/// <summary>
///     A brush that live in render window. Represent a widget.
///     Can futher be used by Painter to modify the labelmap efficiently by caching the brush's active indices.
/// </summary>
public class BrushViewModel : VtkElementViewModel
{
    private readonly vtkPolyDataNormals _brushSmoother = new();

    // Brush shape
    private readonly vtkCylinderSource _brushSource = new();

    // Display
    private readonly vtkPolyDataMapper _mapper = vtkPolyDataMapper.New();
    private readonly vtkTransform _orient = new();
    private readonly vtkTransformPolyDataFilter _orientFilter = new();
    private readonly vtkTransform _position = new();
    private readonly vtkTransformPolyDataFilter _positionFilter = new();

    public BrushViewModel()
    {
        // 1. Create brush geometry (here, a sphere for 3D painting)
        SetDiameter(Diameter);
        SetHeight(Height);
        _brushSource.SetResolution(32);
        _brushSource.Update();

        // 2. Transform the brush to world origin and align its normal based on orientation.
        SetOrientation(Orientation);
        _orientFilter.SetTransform(_orient);
        _orientFilter.SetInputConnection(_brushSource.GetOutputPort());
        _orientFilter.Update();

        // 3. Placing the brush to interested world position
        SetCenter(CenterX, CenterY, CenterZ);
        _positionFilter.SetTransform(_position);
        _positionFilter.SetInputConnection(_orientFilter.GetOutputPort());
        _positionFilter.Update();

        // 4. Smooth the brush by compute its normal.
        _brushSmoother.SetInputConnection(_positionFilter.GetOutputPort());
        _brushSmoother.AutoOrientNormalsOn();
        _brushSmoother.Update();

        // For render window
        _mapper.SetInputConnection(_brushSmoother.GetOutputPort());
        vtkActor? actor = vtkActor.New();
        actor.GetProperty().SetColor(1.0, 0.5, 0.3);
        actor.GetProperty().SetOpacity(0.8);
        actor.GetProperty().SetRepresentationToWireframe();
        actor.GetProperty().SetLineWidth(2);
        actor.SetMapper(_mapper);
        Actor = actor;
    }

    public override vtkActor Actor { get; }

    #region Binable properties

    private double _diameter = 2.0;
    private double _height = 2.0;
    private SliceOrientation _orientation = SliceOrientation.Axial;
    private Vector3 _center = Vector3.Zero;

    public double Diameter
    {
        get => _diameter;
        set
        {
            if (SetField(ref _diameter, value))
            {
                SetDiameter(value);
                OnModified();
            }
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            if (SetField(ref _height, value))
            {
                SetHeight(value);
                OnModified();
            }
        }
    }

    public SliceOrientation Orientation
    {
        get => _orientation;
        set
        {
            if (SetField(ref _orientation, value))
            {
                SetOrientation(value);
                OnModified();
            }
        }
    }

    public float CenterX => _center.X;
    public float CenterY => _center.Y;
    public float CenterZ => _center.Z;

    private void SetDiameter(double diameter)
    {
        _brushSource.SetRadius(diameter / 2.0);
        _brushSource.Modified();
    }

    private void SetHeight(double value)
    {
        _brushSource.SetHeight(value);
        _brushSource.Modified();
    }

    private void SetOrientation(SliceOrientation orientation)
    {
        _orient.Identity();
        switch (orientation)
        {
            case SliceOrientation.Sagittal: _orient.RotateZ(-90); break; // align normal from y to x
            case SliceOrientation.Coronal: /*nothing*/ break; // already y
            case SliceOrientation.Axial: _orient.RotateX(90); break; // align normal from y to z
        }

        _orient.Modified();
    }

    /// <summary>
    ///     For center update, we expose method to update x, y, z concurrently
    /// </summary>
    public void SetCenter(float x, float y, float z)
    {
        _center = new Vector3(x, y, z);
        _position.Identity();
        _position.Translate(x, y, z);
        _positionFilter.Modified();

        OnModified();
        OnPropertyChanged(nameof(CenterX));
        OnPropertyChanged(nameof(CenterY));
        OnPropertyChanged(nameof(CenterZ));
    }

    #endregion
}