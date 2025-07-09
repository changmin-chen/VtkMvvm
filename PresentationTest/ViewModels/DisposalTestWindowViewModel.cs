using System.Reactive;
using System.Reactive.Disposables;
using PresentationTest.TestData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace PresentationTest.ViewModels;

public class DisposalTestWindowViewModel : ReactiveObject
{
    private const string ImagePath = @"TestData\CT_Abdo.nii.gz";
    private readonly SerialDisposable _serialDisposable = new();
    [Reactive] public IReadOnlyList<ImageOrthogonalSliceViewModel> AxialVms { get; private set; }
    public ReactiveCommand<Unit, Unit> LoadNewImageCommand { get; }

    public DisposalTestWindowViewModel()
    {
        LoadNewImageCommand =  ReactiveCommand.CreateFromTask(SwapNewImage);
        
        var image = TestImageLoader.ReadNifti(ImagePath);
        var bgPipe = ColoredImagePipelineBuilder.WithSharedImage(image).Build();
        var axialVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Axial, bgPipe);
        
        AxialVms = [axialVm];
        _serialDisposable.Disposable = axialVm;  // stash for next disposal
    }

    private async Task SwapNewImage()
    {
        var image = await Task.Run(() => TestImageLoader.ReadNifti(ImagePath));
        var bgPipe = ColoredImagePipelineBuilder.WithSharedImage(image).Build();
        var axialVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Axial, bgPipe);
        
        AxialVms = [axialVm];
        _serialDisposable.Disposable = axialVm;

        MessageBox.Show("Loaded new image.");
    }
}