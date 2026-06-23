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
    public int cameraDepth = -100;

    [Header("Advanced Settings")]
    public float nearClipOffset = 0.05f;
    public float nearClipLimit = 0.2f;

    private RenderTexture viewTexture;
    private Camera portalCam;
    private Camera playerCam;
    private List<PortalTraveller> trackedTravellers;
    private MeshFilter screenMeshFilter;

    void Awake()
    {
        playerCam = Camera.main;
        portalCam = GetComponentInChildren<Camera>();
        portalCam.enabled = true;
        portalCam.depth = cameraDepth;
        portalCam.stereoTargetEye = StereoTargetEyeMask.None;

        trackedTravellers = new List<PortalTraveller>();
        screenMeshFilter = screen.GetComponent<MeshFilter>();
        screen.material.SetInt("displayMask", 1);

        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void Update()
    {
        if (playerCam == null)
        {
            playerCam = Camera.main;
            if (playerCam == null)
                foreach (var c in Camera.allCameras)
                    if (c.CompareTag("MainCamera") && c.targetTexture == null && c != portalCam)
                    { playerCam = c; break; }
        }
        if (playerCam == null) return;
        if (!CameraUtility.VisibleFromCamera(linkedPortal.screen, playerCam)) return;

        PositionCamera();
    }

    void PositionCamera()
    {
        CreateViewTexture();
        portalCam.fieldOfView = playerCam.fieldOfView;
        portalCam.nearClipPlane = playerCam.nearClipPlane;
        portalCam.farClipPlane = playerCam.farClipPlane;
        portalCam.aspect = playerCam.aspect;
        portalCam.ResetProjectionMatrix();

        var m = playerCam.transform.localToWorldMatrix;
        for (int i = 0; i < recursionLimit; i++)
        {
            if (i > 0 && !CameraUtility.BoundsOverlap(screenMeshFilter, linkedPortal.screenMeshFilter, portalCam))
                break;
            m = transform.localToWorldMatrix * linkedPortal.transform.worldToLocalMatrix * m;
        }
        portalCam.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
    }

    void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != portalCam) return;
        screen.enabled = false;
        linkedPortal.screen.material.SetInt("displayMask", 0);
    }

    void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != portalCam) return;
        screen.enabled = true;
        linkedPortal.screen.material.SetInt("displayMask", 1);
        if (viewTexture != null)
            linkedPortal.screen.material.SetTexture("_MainTex", viewTexture);
    }

    void LateUpdate() { HandleTravellers(); }

    public void PrePortalRender() { foreach (var t in trackedTravellers) UpdateSliceParams(t); }
    public void Render() { }
    public void PostPortalRender() { }

    void CreateViewTexture()
    {
        if (viewTexture == null || viewTexture.width != Screen.width || viewTexture.height != Screen.height)
        {
            if (viewTexture != null) viewTexture.Release();
            viewTexture = new RenderTexture(Screen.width, Screen.height, 24);
            viewTexture.name = "Portal_" + name;
        }
        portalCam.targetTexture = viewTexture;
    }

    void HandleTravellers()
    {
        for (int i = 0; i < trackedTravellers.Count; i++)
        {
            var t = trackedTravellers[i];
            Transform tt = t.transform;
            var m = linkedPortal.transform.localToWorldMatrix * transform.worldToLocalMatrix * tt.localToWorldMatrix;
            Vector3 off = tt.position - transform.position;
            int s = System.Math.Sign(Vector3.Dot(off, transform.forward));
            int sOld = System.Math.Sign(Vector3.Dot(t.previousOffsetFromPortal, transform.forward));
            if (s != sOld)
            {
                var po = tt.position; var ro = tt.rotation;
                t.Teleport(transform, linkedPortal.transform, m.GetColumn(3), m.rotation);
                if (t.graphicsClone != null) t.graphicsClone.transform.SetPositionAndRotation(po, ro);
                linkedPortal.OnTravellerEnterPortal(t);
                trackedTravellers.RemoveAt(i); i--;
            }
            else
            {
                if (t.graphicsClone != null) t.graphicsClone.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                t.previousOffsetFromPortal = off;
            }
        }
    }

    void UpdateSliceParams(PortalTraveller t)
    {
        if (t.originalMaterials == null || t.originalMaterials.Length == 0) return;
        int side = SideOfPortal(t.transform.position);
        Vector3 sn = transform.forward * -side, cn = linkedPortal.transform.forward * side;
        for (int i = 0; i < t.originalMaterials.Length; i++)
        {
            if (t.originalMaterials[i] != null && t.originalMaterials[i].HasProperty("sliceCentre"))
            { t.originalMaterials[i].SetVector("sliceCentre", transform.position); t.originalMaterials[i].SetVector("sliceNormal", sn); }
            if (t.cloneMaterials != null && i < t.cloneMaterials.Length && t.cloneMaterials[i] != null && t.cloneMaterials[i].HasProperty("sliceCentre"))
            { t.cloneMaterials[i].SetVector("sliceCentre", linkedPortal.transform.position); t.cloneMaterials[i].SetVector("sliceNormal", cn); }
        }
    }

    public void OnTravellerEnterPortal(PortalTraveller t)
    {
        if (!trackedTravellers.Contains(t))
        { t.EnterPortalThreshold(); t.previousOffsetFromPortal = t.transform.position - transform.position; trackedTravellers.Add(t); }
    }

    void OnTriggerEnter(Collider o) { var t = o.GetComponentInParent<PortalTraveller>(); if (t) OnTravellerEnterPortal(t); }
    void OnTriggerExit(Collider o) { var t = o.GetComponentInParent<PortalTraveller>(); if (t != null && trackedTravellers.Contains(t)) { t.ExitPortalThreshold(); trackedTravellers.Remove(t); } }

    int SideOfPortal(Vector3 p) => System.Math.Sign(Vector3.Dot(p - transform.position, transform.forward));
    bool SameSideOfPortal(Vector3 a, Vector3 b) => SideOfPortal(a) == SideOfPortal(b);
    void OnValidate() { if (linkedPortal != null) linkedPortal.linkedPortal = this; }
    void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        if (viewTexture != null) { viewTexture.Release(); viewTexture = null; }
    }
}