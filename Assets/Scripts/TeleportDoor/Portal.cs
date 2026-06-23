using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(BoxCollider))]
public class Portal : MonoBehaviour
{
    [Header("Main Settings")]
    public Portal linkedPortal;
    public MeshRenderer screen;
    public int recursionLimit = 5;

    [Header("Advanced Settings")]
    public float nearClipOffset = 0.05f;
    public float nearClipLimit = 0.2f;

    // Internal
    private RenderTexture viewTexture;
    private Camera portalCam;
    private Camera playerCam;
    private List<PortalTraveller> trackedTravellers;
    private MeshFilter screenMeshFilter;

    void Awake()
    {
        playerCam = Camera.main;
        portalCam = GetComponentInChildren<Camera>();
        // URP auto-render: enabled camera renders to targetTexture
        portalCam.enabled = true;
        portalCam.depth = -100;
        trackedTravellers = new List<PortalTraveller>();
        screenMeshFilter = screen.GetComponent<MeshFilter>();
        screen.material.SetInt("displayMask", 1);

        // Register URP render callbacks
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void Update()
    {
        // VR may not have Camera.main ready in Awake
        if (playerCam == null)
        {
            playerCam = Camera.main;
            if (playerCam == null)
            {
                var allCams = Camera.allCameras;
                foreach (var c in allCams)
                {
                    if (c.CompareTag("MainCamera") && c.targetTexture == null)
                    { playerCam = c; break; }
                }
            }
        }
    }

    void LateUpdate()
    {
        HandleTravellers();
    }

    // ============================================================
    // URP Render Callbacks — camera position set BEFORE URP renders
    // ============================================================

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        // Only handle this portal's own camera
        if (cam != portalCam) return;
        if (playerCam == null) return;

        // Skip if player can't see the linked portal's screen
        if (!CameraUtility.VisibleFromCamera(linkedPortal.screen, playerCam)) return;

        CreateViewTexture();
        portalCam.targetTexture = viewTexture;

        // ---- Calculate camera position (same math as reference) ----
        var localToWorldMatrix = playerCam.transform.localToWorldMatrix;
        portalCam.projectionMatrix = playerCam.projectionMatrix;

        for (int i = 0; i < recursionLimit; i++)
        {
            if (i > 0)
            {
                if (!CameraUtility.BoundsOverlap(screenMeshFilter, linkedPortal.screenMeshFilter, portalCam))
                    break;
            }
            localToWorldMatrix = transform.localToWorldMatrix * linkedPortal.transform.worldToLocalMatrix * localToWorldMatrix;
        }

        portalCam.transform.SetPositionAndRotation(localToWorldMatrix.GetColumn(3), localToWorldMatrix.rotation);
        SetNearClipPlane();
        HandleClipping();

        // ---- Hide screens so portal camera can see through ----
        // This portal's screen becomes transparent (shadows only)
        screen.enabled = false;
        // Linked portal's display is masked out
        linkedPortal.screen.material.SetInt("displayMask", 0);
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam != portalCam) return;

        // Restore screen visibility
        screen.enabled = true;
        linkedPortal.screen.material.SetInt("displayMask", 1);

        // Assign rendered texture to linked portal's screen
        if (viewTexture != null)
            linkedPortal.screen.material.SetTexture("_MainTex", viewTexture);
    }

    // ============================================================
    // Public API — called by MainCamera (kept for compatibility,
    // actual rendering is now handled by URP callbacks above)
    // ============================================================

    public void PrePortalRender()
    {
        foreach (var traveller in trackedTravellers)
            UpdateSliceParams(traveller);
    }

    public void Render()
    {
        // URP auto-render handles actual rendering via OnBeginCameraRendering.
        // This method kept for external callers but is now a no-op.
    }

    public void PostPortalRender()
    {
        if (playerCam != null)
            ProtectScreenFromClipping(playerCam.transform.position);
    }

    // ============================================================
    // Internal helpers
    // ============================================================

    void CreateViewTexture()
    {
        if (viewTexture == null || viewTexture.width != Screen.width || viewTexture.height != Screen.height)
        {
            if (viewTexture != null)
                viewTexture.Release();

            viewTexture = new RenderTexture(Screen.width, Screen.height, 0);
            portalCam.targetTexture = viewTexture;
        }
    }

    void HandleClipping()
    {
        float screenThickness = linkedPortal.ProtectScreenFromClipping(portalCam.transform.position);

        bool camFacingSameDirAsPortal = Vector3.Dot(transform.forward, transform.position - portalCam.transform.position) > 0;
        screen.transform.localScale = new Vector3(screen.transform.localScale.x, screen.transform.localScale.y, screenThickness);
        screen.transform.localPosition = Vector3.forward * screenThickness * ((camFacingSameDirAsPortal) ? 0.5f : -0.5f);
    }

    float ProtectScreenFromClipping(Vector3 viewPoint)
    {
        float halfHeight = playerCam.nearClipPlane * Mathf.Tan(playerCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfWidth = halfHeight * playerCam.aspect;
        float dstToNearClipPlaneCorner = new Vector3(halfWidth, halfHeight, playerCam.nearClipPlane).magnitude;
        float screenThickness = dstToNearClipPlaneCorner;

        Transform screenT = screen.transform;
        bool camFacingSameDirAsPortal = Vector3.Dot(transform.forward, transform.position - viewPoint) > 0;
        screenT.localScale = new Vector3(screenT.localScale.x, screenT.localScale.y, screenThickness);
        screenT.localPosition = Vector3.forward * screenThickness * ((camFacingSameDirAsPortal) ? 0.5f : -0.5f);
        return screenThickness;
    }

    // ============================================================
    // Traveller handling + teleport
    // ============================================================

    void HandleTravellers()
    {
        for (int i = 0; i < trackedTravellers.Count; i++)
        {
            PortalTraveller traveller = trackedTravellers[i];
            Transform travellerT = traveller.transform;
            var m = linkedPortal.transform.localToWorldMatrix * transform.worldToLocalMatrix * travellerT.localToWorldMatrix;

            Vector3 offsetFromPortal = travellerT.position - transform.position;
            int portalSide = System.Math.Sign(Vector3.Dot(offsetFromPortal, transform.forward));
            int portalSideOld = System.Math.Sign(Vector3.Dot(traveller.previousOffsetFromPortal, transform.forward));

            if (portalSide != portalSideOld)
            {
                var positionOld = travellerT.position;
                var rotOld = travellerT.rotation;
                traveller.Teleport(transform, linkedPortal.transform, m.GetColumn(3), m.rotation);
                if (traveller.graphicsClone != null)
                    traveller.graphicsClone.transform.SetPositionAndRotation(positionOld, rotOld);

                linkedPortal.OnTravellerEnterPortal(traveller);
                trackedTravellers.RemoveAt(i);
                i--;
            }
            else
            {
                if (traveller.graphicsClone != null)
                    traveller.graphicsClone.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                traveller.previousOffsetFromPortal = offsetFromPortal;
            }
        }
    }

    void UpdateSliceParams(PortalTraveller traveller)
    {
        if (traveller.originalMaterials == null || traveller.originalMaterials.Length == 0) return;

        int side = SideOfPortal(traveller.transform.position);
        Vector3 sliceNormal = transform.forward * -side;
        Vector3 cloneSliceNormal = linkedPortal.transform.forward * side;

        Vector3 slicePos = transform.position;
        Vector3 cloneSlicePos = linkedPortal.transform.position;

        float sliceOffsetDst = 0;
        float cloneSliceOffsetDst = 0;
        float screenThickness = screen.transform.localScale.z;

        bool playerSameSideAsTraveller = SameSideOfPortal(playerCam.transform.position, traveller.transform.position);
        if (!playerSameSideAsTraveller) sliceOffsetDst = -screenThickness;

        bool playerSameSideAsCloneAppearing = side != linkedPortal.SideOfPortal(playerCam.transform.position);
        if (!playerSameSideAsCloneAppearing) cloneSliceOffsetDst = -screenThickness;

        for (int i = 0; i < traveller.originalMaterials.Length; i++)
        {
            var mat = traveller.originalMaterials[i];
            if (mat != null && mat.HasProperty("sliceCentre"))
            {
                mat.SetVector("sliceCentre", slicePos);
                mat.SetVector("sliceNormal", sliceNormal);
                mat.SetFloat("sliceOffsetDst", sliceOffsetDst);
            }
            if (traveller.cloneMaterials != null && i < traveller.cloneMaterials.Length)
            {
                var cloneMat = traveller.cloneMaterials[i];
                if (cloneMat != null && cloneMat.HasProperty("sliceCentre"))
                {
                    cloneMat.SetVector("sliceCentre", cloneSlicePos);
                    cloneMat.SetVector("sliceNormal", cloneSliceNormal);
                    cloneMat.SetFloat("sliceOffsetDst", cloneSliceOffsetDst);
                }
            }
        }
    }

    void SetNearClipPlane()
    {
        Transform clipPlane = transform;
        int dot = System.Math.Sign(Vector3.Dot(clipPlane.forward, transform.position - portalCam.transform.position));

        Vector3 camSpacePos = portalCam.worldToCameraMatrix.MultiplyPoint(clipPlane.position);
        Vector3 camSpaceNormal = portalCam.worldToCameraMatrix.MultiplyVector(clipPlane.forward) * dot;
        float camSpaceDst = -Vector3.Dot(camSpacePos, camSpaceNormal) + nearClipOffset;

        if (Mathf.Abs(camSpaceDst) > nearClipLimit)
        {
            Vector4 clipPlaneCameraSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDst);
            portalCam.projectionMatrix = playerCam.CalculateObliqueMatrix(clipPlaneCameraSpace);
        }
        else
        {
            portalCam.projectionMatrix = playerCam.projectionMatrix;
        }
    }

    public void OnTravellerEnterPortal(PortalTraveller traveller)
    {
        if (!trackedTravellers.Contains(traveller))
        {
            traveller.EnterPortalThreshold();
            traveller.previousOffsetFromPortal = traveller.transform.position - transform.position;
            trackedTravellers.Add(traveller);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var traveller = other.GetComponentInParent<PortalTraveller>();
        if (traveller != null)
            OnTravellerEnterPortal(traveller);
    }

    void OnTriggerExit(Collider other)
    {
        var traveller = other.GetComponentInParent<PortalTraveller>();
        if (traveller != null && trackedTravellers.Contains(traveller))
        {
            traveller.ExitPortalThreshold();
            trackedTravellers.Remove(traveller);
        }
    }

    int SideOfPortal(Vector3 pos) => System.Math.Sign(Vector3.Dot(pos - transform.position, transform.forward));
    bool SameSideOfPortal(Vector3 posA, Vector3 posB) => SideOfPortal(posA) == SideOfPortal(posB);

    void OnValidate()
    {
        if (linkedPortal != null)
            linkedPortal.linkedPortal = this;
    }

    void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

        if (viewTexture != null)
        {
            viewTexture.Release();
            viewTexture = null;
        }
    }
}
