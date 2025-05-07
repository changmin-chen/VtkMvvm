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

    public BrushViewModel()
    {
        // 1. Create brush geometry (here, a sphere for 3D painting)
        SetBrushDiameter(Diameter);
        SetBrushHeight(Height);
        _brushSource.Update();

        // 2. Transform the brush to world origin and align its normal based on orientation.
        _orient.Identity();
        SetBrushOrientation(Orientation);
        _orientFilter.SetTransform(_orient);
        _orientFilter.SetInputConnection(_brushSource.GetOutputPort());
        _orientFilter.Update();

        // 3. Smooth the brush by compute its normal.
        _brushSmoother.SetInputConnection(_orientFilter.GetOutputPort());
        _brushSmoother.AutoOrientNormalsOn();
        _brushSmoother.Update();

        // For render window
        _mapper.SetInputConnection(_brushSmoother.GetOutputPort());
        vtkActor? actor = vtkActor.New();
        actor.GetProperty().SetColor(1.0, 0.5, 0.3);
        actor.GetProperty().SetOpacity(0.8);
        actor.SetMapper(_mapper);
        Actor = actor;
    }

    public override vtkActor Actor { get; }

    #region Binable properties

    private double _diameter = 5.0;
    private double _height = 2.0;
    private SliceOrientation _orientation = SliceOrientation.Axial;

    public double Diameter
    {
        get => _diameter;
        set
        {
            if (SetField(ref _diameter, value))
            {
                SetBrushDiameter(value);
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
                SetBrushHeight(value);
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
                SetBrushOrientation(value);
            }
        }
    }

    private void SetBrushDiameter(double diameter)
    {
        _brushSource.SetRadius(diameter / 2.0);
        _brushSource.Modified();
    }

    private void SetBrushHeight(double value)
    {
        _brushSource.SetHeight(value);
        _brushSource.Modified();
    }

    private void SetBrushOrientation(SliceOrientation orientation)
    {
        switch (orientation)
        {
            case SliceOrientation.Sagittal: _orient.RotateZ(-90); break; // align normal from y to x
            case SliceOrientation.Coronal: /*nothing*/ break; // already y
            case SliceOrientation.Axial: _orient.RotateX(90); break; // align normal from y to z
        }

        _orient.Modified();
    }

    #endregion
}