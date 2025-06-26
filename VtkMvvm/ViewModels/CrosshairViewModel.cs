using Kitware.VTK;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels;

/// <summary>
///     A crosshair widget that can be rendered and overlay onto a background image.
///     The crossing position should sync with the user-selected world coordinate to provide the visual hint.
/// </summary>
public class CrosshairViewModel : VtkElementViewModel
{
    private readonly double[] _bounds; // based on background image volume to decides line boundary

    private readonly vtkLineSource _hLine = vtkLineSource.New();
    private readonly vtkLineSource _vLine = vtkLineSource.New();
    private readonly vtkAppendPolyData _append = vtkAppendPolyData.New();
    private readonly vtkPolyDataMapper _mapper = vtkPolyDataMapper.New();
    private readonly vtkActor _actor = vtkActor.New();

    private Double3 _crossingPosition;

    public CrosshairViewModel(SliceOrientation orientation, double[] imageBounds)
    {
        Orientation = orientation;
        _bounds = imageBounds;

        // build pipeline: H-line + V-line → append → mapper → actor
        _append.AddInputConnection(_hLine.GetOutputPort());
        _append.AddInputConnection(_vLine.GetOutputPort());

        _mapper.SetInputConnection(_append.GetOutputPort());
        _actor.SetMapper(_mapper);
        _actor.GetProperty().SetColor(1, 0, 0);
        _actor.GetProperty().SetLineWidth(1.5F);
    }

    public SliceOrientation Orientation { get; }
    public override vtkProp Actor => _actor;


    #region Bindable Properties

    public Double3 CrossingPosition
    {
        get => _crossingPosition;
        set
        {
            if (SetField(ref _crossingPosition, value))
            {
                UpdateCrosshair(value);
                OnModified();
            }
        }
    }

    #endregion

    private void UpdateCrosshair(Double3 worldPosition)
    {
        (double wx, double wy, double wz) = worldPosition;
        switch (Orientation)
        {
            case SliceOrientation.Axial: //  Z is fixed
                _hLine.SetPoint1(_bounds[0], wy, wz);
                _hLine.SetPoint2(_bounds[1], wy, wz);
                _vLine.SetPoint1(wx, _bounds[2], wz);
                _vLine.SetPoint2(wx, _bounds[3], wz);
                break;
            case SliceOrientation.Coronal: //  Y is fixed
                _hLine.SetPoint1(_bounds[0], wy, _bounds[4]);
                _hLine.SetPoint2(_bounds[1], wy, _bounds[4]);
                _vLine.SetPoint1(wx, wy, _bounds[4]);
                _vLine.SetPoint2(wx, wy, _bounds[5]);
                break;

            case SliceOrientation.Sagittal: //  X is fixed
                _hLine.SetPoint1(wx, _bounds[2], _bounds[4]);
                _hLine.SetPoint2(wx, _bounds[3], _bounds[4]);
                _vLine.SetPoint1(wx, wy, _bounds[4]);
                _vLine.SetPoint2(wx, wy, _bounds[5]);
                break;
        }

        _hLine.Modified();
        _vLine.Modified();
    }
}