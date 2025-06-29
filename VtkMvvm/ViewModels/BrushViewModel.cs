using Kitware.VTK;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels;

/// <summary>
///     A 3D brush widget that renders in the VTK window and can be used for interactive painting.
/// </summary>
public class BrushViewModel : VtkElementViewModel
{
    private readonly vtkCylinderSource _brushSource = new();
    private readonly vtkPolyDataNormals _brushSmoother = new();
    private readonly vtkPolyDataMapper _mapper = vtkPolyDataMapper.New();
    private readonly vtkTransform _orient = new();
    private readonly vtkTransformPolyDataFilter _orientFilter = new();
    private readonly vtkTransform _position = new();
    private readonly vtkTransformPolyDataFilter _positionFilter = new();

    private double _diameter = 2.0;
    private double _height = 2.0;
    private SliceOrientation _orientation = SliceOrientation.Axial;
    private Double3 _center = Double3.Zero;

    public BrushViewModel()
    {
        // 1. Create brush geometry (here, a sphere for 3D painting)
        SetDiameter(Diameter);
        SetHeight(Height);
        _brushSource.SetResolution(32);

        // 2. Transform the brush to world origin and align its normal based on orientation.
        SetOrientation(Orientation);
        _orientFilter.SetTransform(_orient);
        _orientFilter.SetInputConnection(_brushSource.GetOutputPort());

        // 3. Placing the brush to interested world position
        _positionFilter.SetTransform(_position);
        _positionFilter.SetInputConnection(_orientFilter.GetOutputPort());

        // 4. Smooth the brush by compute its normal.
        _brushSmoother.SetInputConnection(_positionFilter.GetOutputPort());
        _brushSmoother.AutoOrientNormalsOn();

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

    /// <summary>
    ///     Get the port that output the rotated brush <see cref="vtkPolyData" /> but with its center at (0, 0, 0).
    /// </summary>
    public vtkAlgorithmOutput GetBrushGeometryPort() => _orientFilter.GetOutputPort();


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

    #region Binable properties

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

    public Double3 Center
    {
        get => _center;
        set
        {
            if (SetField(ref _center, value))
            {
                _position.Identity();
                _position.Translate(value.X, value.Y, value.Z);
                _positionFilter.Modified();
                OnModified();
            }
        }
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose of the resource specific to this derived class
            _brushSource.Dispose();
            _brushSmoother.Dispose();
            _mapper.Dispose();
            _orient.Dispose();
            _orientFilter.Dispose();
            _position.Dispose();
            _positionFilter.Dispose();
        }
        // IMPORTANT: Call the base class implementation
        base.Dispose(disposing);
    }
}