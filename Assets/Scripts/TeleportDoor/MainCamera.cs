using UnityEngine;

public class MainCamera : MonoBehaviour
{
    Portal[] portals;

    void Start() { portals = FindObjectsOfType<Portal>(); }

    void Update()
    {
        if (portals == null || portals.Length == 0)
            portals = FindObjectsOfType<Portal>();
        if (portals == null) return;
        for (int i = 0; i < portals.Length; i++)
            if (portals[i] != null && portals[i].isActiveAndEnabled)
                portals[i].PrePortalRender();
    }
}