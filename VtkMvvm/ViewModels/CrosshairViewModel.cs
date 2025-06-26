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

    private Double3 _focalPoint;

    public CrosshairViewModel(SliceOrientation orientation, double[] imageBounds)
    {
        Orientation = orientation;
        _bounds = imageBounds;

        // build pipeline: H-line + V-line → append → mapper → actor
        _append.AddInputConnection(_hLine.GetOutputPort());
        _append.AddInputConnection(_vLine.GetOutputPort());
        _mapper.SetInputConnection(_append.GetOutputPort());

        vtkActor? actor = vtkActor.New();
        actor.SetMapper(_mapper);
        actor.GetProperty().SetColor(1, 0, 0);
        actor.GetProperty().SetLineWidth(1.5F);
        Actor = actor;
    }

    public SliceOrientation Orientation { get; }
    public override vtkActor Actor { get; }

    #region Bindable Properties

    public Double3 FocalPoint
    {
        get => _focalPoint;
        set
        {
            if (SetField(ref _focalPoint, value))
            {
                SetFocalPoint(value);
                _append.Modified();
                OnModified();
            }
        }
    }

    #endregion

    private void SetFocalPoint(Double3 worldPosition)
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
                _hLine.SetPoint1(_bounds[0], wy, wz);
                _hLine.SetPoint2(_bounds[1], wy, wz);
                _vLine.SetPoint1(wx, wy, _bounds[4]);
                _vLine.SetPoint2(wx, wy, _bounds[5]);
                break;

            case SliceOrientation.Sagittal: //  X is fixed
                _hLine.SetPoint1(wx, _bounds[2], wz);
                _hLine.SetPoint2(wx, _bounds[3], wz);
                _vLine.SetPoint1(wx, wy, _bounds[4]);
                _vLine.SetPoint2(wx, wy, _bounds[5]);
                break;
        }

        _hLine.Modified();
        _vLine.Modified();
    }
}