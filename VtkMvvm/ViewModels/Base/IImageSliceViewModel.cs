using System.Numerics;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels.Base;

/// <summary>
/// Slice orientation info of the sliced 2D image plane in the 3D space.
/// Useful for camera positioning.
/// </summary>
public interface IImageSliceViewModel
{
    Vector3 PlaneNormal { get; }
    Vector3 PlaneAxisU { get; }
    Vector3 PlaneAxisV { get; }
    Double3 PlaneOrigin { get; }
    
    // -- change the displayed slice -----------------------
    int SliceIndex { get; set; } 
}
