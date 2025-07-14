using System.Numerics;
using Kitware.VTK;

namespace VtkMvvm.Controls;

/// <summary>
/// Abstraction of a VTK-backed display surface.
/// <para>
/// <b>Ownership semantics:</b>
///   • All <see cref="vtkObject"/>s returned as <em>properties</em> are
///     <c>strong</c> references – their lifetime is managed by the
///     <see cref="IVtkSceneControl"/> implementer.
///   • Objects returned by the <c>Get…</c> methods are <em>borrowed</em>
///     – callers must not Dispose or hold on to them
///     beyond the current rendering frame.
/// </para>
/// </summary>
public interface IVtkSceneControl
{
    public vtkRenderer MainRenderer { get; }
    public vtkRenderer OverlayRenderer { get; }
    public vtkRenderWindowInteractor GetInteractor();


    /// <summary>unit vector defined as "camera position - focal point", which points towards the camera.</summary>
    public Vector3 GetViewPlaneNormal()
    {
        double[] vpn = MainRenderer.GetActiveCamera().GetViewPlaneNormal();
        return new Vector3((float)vpn[0], (float)vpn[1], (float)vpn[2]);
    }
}