using System.Diagnostics;
using System.Reactive;
using System.Reactive.Disposables;
using Kitware.VTK;
using PresentationTest.TestData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using VtkMvvm.Controls;
using VtkMvvm.Extensions;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace PresentationTest.ViewModels;

public class DisposalTestWindowViewModel : ReactiveObject
{
    private const string ImagePath = @"TestData\CT_Abdo.nii.gz";
    private readonly SerialDisposable _serialDisposable = new();
    private readonly vtkCellPicker _picker = new();
    private vtkImageData _background;

    [Reactive] public IReadOnlyList<ImageOrthogonalSliceViewModel> AxialVms { get; private set; }
    [Reactive] public IReadOnlyList<ImageOrthogonalSliceViewModel> CoronalVms { get; private set; }
    public ReactiveCommand<Unit, Unit> LoadNewImageCommand { get; }

    public DisposalTestWindowViewModel()
    {
        LoadNewImageCommand = ReactiveCommand.CreateFromTask(SwapNewImage);

        var image = TestImageLoader.ReadNifti(ImagePath);
        var bgPipe = ColoredImagePipelineBuilder.WithSharedImage(image).Build();
        var axialVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Axial, bgPipe);
        var coronalVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Coronal, bgPipe);
        _background = image;
        AxialVms = [axialVm];
        CoronalVms = [coronalVm];
        _serialDisposable.Disposable = new CompositeDisposable(axialVm, coronalVm); 
    }

    // ------ Public to View -----------------------//
    public void OnControlGetMouseDisplayPosition(IVtkSceneControl sender, int x, int y)
    {
        if (_picker.Pick(x, y, 0, sender.MainRenderer) == 0) return;
        Double3 clickWorldPos = _picker.GetPickWorldPosition();

        if (_background.TryComputeStructuredCoordinates(clickWorldPos, out var voxel, out Double3 _))
        {
            AxialVms[0].SliceIndex = voxel.k;
            CoronalVms[0].SliceIndex = voxel.j;
        }
    }

    private async Task SwapNewImage()
    {
        var image = await Task.Run(() => TestImageLoader.ReadNifti(ImagePath));
        BuildAndSwapViewModels(image);

        MessageBox.Show("Loaded new image.");
    }

    /// <summary>
    /// Test the disposal of last ViewModels instances.
    /// </summary>
    /// <param name="image">New image to build the rendering pipeline</param>
    private void BuildAndSwapViewModels(vtkImageData image)
    {
        var oldAxialVm = AxialVms[0];
        Debug.Assert(!oldAxialVm.IsDisposed, "Not disposed yet.");
        
        // Build new ViewModels
        var bgPipe = ColoredImagePipelineBuilder.WithSharedImage(image).Build();
        var axialVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Axial, bgPipe);
        var coronalVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Coronal, bgPipe);
        
        // Swap to new 
        _background = image;
        AxialVms = [axialVm];
        CoronalVms = [coronalVm];
        _serialDisposable.Disposable = new CompositeDisposable(axialVm, coronalVm); // stash for next disposal

        // Debug assert
        Debug.Assert(oldAxialVm.IsDisposed, "Should be disposed.");
    }
}