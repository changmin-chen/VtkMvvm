﻿using Kitware.VTK;
using VtkMvvm.ViewModels.Base;

namespace VtkMvvm.Features.Builder;

/// <summary>
/// DTO for the shared vtk image pipeline with color mapping.
/// The image data may be shared across multiple <see cref="VtkElementViewModel"/>.
/// So the ViewModel should not dispose them. 
/// </summary>
public sealed record ColoredImagePipeline(
    vtkImageData Image,
    vtkLookupTable LookupTable,
    bool IsRgba,
    bool IsLinearInterpolationOn
)
{
    /// <summary>
    ///     Connect pipeline: Image -> ColorMap -> Actor
    /// </summary>
    internal void Connect(vtkImageMapToColors mapToColors, vtkImageActor actor)
    {
        ConfigureColormap(mapToColors);

        mapToColors.SetInput(Image);
        actor.SetInput(mapToColors.GetOutput());

        if (IsLinearInterpolationOn) actor.InterpolateOn();
        else actor.InterpolateOff();
    }

    /// <summary>
    /// Connect pipeline: Image -> Reslice Image → ColorMap → Actor
    /// </summary>
    internal void ConnectWithReslice(vtkImageMapToColors mapToColors, vtkImageReslice reslice, vtkImageActor actor)
    {
        ConfigureColormap(mapToColors);

        reslice.SetInput(Image);
        mapToColors.SetInputConnection(reslice.GetOutputPort());
        actor.SetInput(mapToColors.GetOutput());

        if (IsLinearInterpolationOn) actor.InterpolateOn();
        else actor.InterpolateOff();
    }

    private void ConfigureColormap(vtkImageMapToColors mapToColors)
    {
        mapToColors.SetLookupTable(LookupTable);

        if (IsRgba)
        {
            mapToColors.SetOutputFormatToRGBA();
        }
        else
        {
            mapToColors.SetOutputFormatToLuminance();
        }
    }
}