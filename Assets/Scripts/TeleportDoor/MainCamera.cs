using UnityEngine;

/// <summary>
/// Drive portal slice-param updates each frame.
/// Actual rendering is handled by Portal'secret URP callbacks (RenderPipelineManager).
/// Attach to any GameObject in the scene (e.g. the player camera).
/// </summary>
public class MainCamera : MonoBehaviour
{
    private Portal[] portals;

    void Start()
    {
        portals = FindObjectsOfType<Portal>();
    }

    void Update()
    {
        // Refresh portal list in case portals are spawned dynamically
        if (portals == null || portals.Length == 0)
            portals = FindObjectsOfType<Portal>();

        // PrePortalRender sets slice params for objects near portal thresholds
        // Must run in Update (not LateUpdate) so params are set before URP renders
        if (portals == null) return;
        for (int i = 0; i < portals.Length; i++)
            if (portals[i] != null && portals[i].isActiveAndEnabled)
                portals[i].PrePortalRender();
    }
}