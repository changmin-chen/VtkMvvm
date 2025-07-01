using System.Numerics;
using VtkMvvm.Models;

namespace VtkMvvm.ViewModels.Base;

/// <summary>
/// Spatial information of the sliced 2D image plane in the 3D space
/// </summary>
public interface ISlicePlaneInfo
{
    Vector3 PlaneNormal { get; }
    Vector3 PlaneAxisU { get; }
    Vector3 PlaneAxisV { get; }
    Double3 PlaneOrigin { get; }
}