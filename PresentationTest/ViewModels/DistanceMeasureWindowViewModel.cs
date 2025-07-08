using Kitware.VTK;
using PresentationTest.TestData;
using ReactiveUI;
using VtkMvvm.Features.Builder;
using VtkMvvm.Models;
using VtkMvvm.ViewModels;

namespace PresentationTest.ViewModels;

public class DistanceMeasureWindowViewModel : ReactiveObject
{
    private readonly vtkImageData _background;
    public ImageOrthogonalSliceViewModel AxialVm { get; }
    public ImageOrthogonalSliceViewModel[] AxialVms => [AxialVm];
    

    public DistanceMeasureWindowViewModel()
    {
        _background = TestImageLoader.ReadNifti(@"TestData\CT_Abdo.nii.gz");
        var bgPipe = ColoredImagePipelineBuilder
        
            .WithSharedImage(_background)
            .Build();
        AxialVm = new ImageOrthogonalSliceViewModel(SliceOrientation.Axial, bgPipe);
    }

    
}
