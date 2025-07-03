using System.Numerics;
using Kitware.VTK;

namespace VtkMvvm.Controls;

public interface IVtkSceneControl
{
    public vtkRenderer MainRenderer { get; }
    public vtkRenderer OverlayRenderer { get; }
    public vtkRenderWindowInteractor Interactor { get; }
    
    // ── camera orientation info ─────────────────────
    public vtkCamera Camera => MainRenderer.GetActiveCamera();

    /// <summary>unit vector defined as "camera position - focal point", which points towards the camera.</summary>
    public Vector3 GetViewPlaneNormal()
    {
        double[] vpn = Camera.GetViewPlaneNormal();
        return new Vector3((float)vpn[0], (float)vpn[1], (float)vpn[2]);
    }
}