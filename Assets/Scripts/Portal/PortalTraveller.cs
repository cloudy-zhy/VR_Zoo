using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂载到 XR Origin / XR Rig 根节点上。
/// Teleport 时会自动补偿相机偏移量，确保玩家相机落在正确位置。
/// </summary>
public class PortalTraveller : MonoBehaviour
{
    [Header("Visual Clone (optional)")]
    [Tooltip("有身体模型才填")]
    public GameObject graphicsObject;

    [Header("VR Camera Reference")]
    [Tooltip("拖入 Camera Offset（XR Origin → Camera Offset → Main Camera 的中间层）。用 Camera Offset 而非 MainCamera，因为 MainCamera 的 transform 被 VR tracking 覆盖")]
    public Transform cameraTransform;

    public GameObject graphicsClone { get; set; }
    public Vector3 previousOffsetFromPortal { get; set; }
    public Material[] originalMaterials { get; set; }
    public Material[] cloneMaterials { get; set; }

    public virtual void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
    {
        if (cameraTransform != null)
        {
            cameraTransform.position += pos - transform.position;
            // cameraTransform.rotation = rot;
        }
        else
        {
            // 简单模式：直接移动
            transform.position = pos;
            transform.rotation = rot;
        }
    }

    public virtual void EnterPortalThreshold()
    {
        if (graphicsObject == null) return;

        if (graphicsClone == null)
        {
            graphicsClone = Instantiate(graphicsObject);
            graphicsClone.transform.SetParent(graphicsObject.transform.parent);
            graphicsClone.transform.localScale = graphicsObject.transform.localScale;
            originalMaterials = GetMaterials(graphicsObject);
            cloneMaterials = GetMaterials(graphicsClone);
        }
        else
        {
            graphicsClone.SetActive(true);
        }
    }

    public virtual void ExitPortalThreshold()
    {
        if (graphicsClone != null)
            graphicsClone.SetActive(false);

        if (originalMaterials != null)
        {
            for (int i = 0; i < originalMaterials.Length; i++)
            {
                var mat = originalMaterials[i];
                if (mat == null || !mat.HasProperty("sliceNormal")) continue;
                mat.SetVector("sliceNormal", Vector3.zero);
            }
        }
    }

    Material[] GetMaterials(GameObject g)
    {
        var renderers = g.GetComponentsInChildren<MeshRenderer>();
        var matList = new List<Material>();
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                matList.Add(mat);
            }
        }
        return matList.ToArray();
    }
}
