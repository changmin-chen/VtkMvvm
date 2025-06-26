using System.Reactive.Disposables;
using System.Windows;
using Kitware.VTK;
using VtkMvvm.Controls;

namespace PresentationTest;

public partial class VtkObliqueSliceTestWindow : Window
{
    private readonly CompositeDisposable _disposables = new();
    private VtkObliqueSliceTestWindowViewModel _vm;
    public VtkObliqueSliceTestWindow()
    {
        InitializeComponent();
        Loaded += OnLoadedOnce;
    }
    private void OnLoadedOnce(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedOnce;

        if (DataContext is VtkObliqueSliceTestWindowViewModel vm)
        {
            _vm = vm;
        }
        InitializeInteractor(ObliqueControl);
    }

    /// <summary>
    /// Each controls has their own instance of freehand interactor
    /// </summary>
    private void InitializeInteractor(VtkObliqueImageSceneControl control)
    {
        vtkInteractorStyleImage style = new();
        vtkRenderWindowInteractor? iren = control.RenderWindowControl.RenderWindow.GetInteractor();
        iren.SetInteractorStyle(style);
        iren.Initialize();
    }
}