using System.Reactive.Disposables;
using Kitware.VTK;
using VtkMvvm.ViewModels.Base;

namespace VtkMvvm.ViewModels;

/// <summary>A single ruler (distance widget) that lives on one image slice.</summary>
public sealed class DistanceMeasureViewModel : ViewModelBase, IDisposable
{
    // --- public reactive state -------------------------------------------------
    private double _distance;

    public double Distance
    {
        get => _distance;
        private set => SetField(ref _distance, value);
    }

    public bool IsFrozen => _widget.GetProcessEvents() == 1;

    // --- VTK plumbing ----------------------------------------------------------
    readonly vtkDistanceWidget _widget;
    readonly vtkDistanceRepresentation2D _rep;
    readonly CompositeDisposable _disposables = new();

    public DistanceMeasureViewModel(vtkRenderWindowInteractor iren,
        vtkImageActor imageSliceActor)
    {
        // 1) placer keeps the handles stuck to the slice plane
        var placer = vtkImageActorPointPlacer.New();
        placer.SetImageActor(imageSliceActor);

        // 2) distance representation with the two handle reps
        _rep = vtkDistanceRepresentation2D.New();
        //     in VTK 5.8 we set the placer on each handle explicitly
        _rep.GetPoint1Representation().SetPointPlacer(placer);
        _rep.GetPoint2Representation().SetPointPlacer(placer);

        // cosmetics
        _rep.GetAxisProperty().SetColor(1, 1, 0); // yellow line
        _rep.SetLabelFormat("%4.2f mm"); // “42.37 mm”

        // 3) the widget itself
        _widget = vtkDistanceWidget.New();
        _widget.SetInteractor(iren);
        _widget.SetRepresentation(_rep);
        _widget.CreateDefaultRepresentation();
        _widget.EnabledOn(); // start listening to the mouse

        // wire VTK → VM
        // EndInteractionEvent fires when user releases the mouse after moving either handle
        _widget.EndInteractionEvt += (_, __) => UpdateDistance();

        UpdateDistance();

        // cleanup chain
        _disposables.Add(Disposable.Create(() =>
        {
            _widget.EnabledOff();
            _widget.Dispose();
            _rep.Dispose();
            placer.Dispose();
        }));
    }


    void UpdateDistance() => Distance = _rep.GetDistance();

    /// <summary>Stops user interaction but keeps the ruler visible.</summary>
    public void Freeze() => _widget.ProcessEventsOff();

    public void Dispose() => _disposables.Dispose();
}