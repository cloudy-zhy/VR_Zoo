using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using UnityEngine.Playables;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum AnimalType
{
    Dodo, Moose, Mammoth, Zuolong, LittleDragonHunter, LiangLong, Triceratops, Pterodactyl
}

/// <summary>
/// 单个动物的材质替换配置。
/// targetMaterials 按顺序对应 animalObject 下所有 MeshRenderer 的 sharedMaterials 槽位。
/// </summary>
[System.Serializable]
public class AnimalMaterialEntry
{
    [Tooltip("动物类型")]
    public AnimalType animalType;

    [Tooltip("动物根 GameObject（会查找自身及子物体的所有 MeshRenderer）")]
    public GameObject animalObject;

    [Tooltip("目标材质数组，按 MeshRenderer 顺序逐槽位对应")]
    public Material[] targetMaterials;
}

public class CeremonyEffectsController : MonoBehaviour
{
    #region SerializedFields

    [Header("Animal Materials")]
    [SerializeField] private List<AnimalMaterialEntry> animalMaterials = new List<AnimalMaterialEntry>();

    [Header("hoshi sora")]
    [SerializeField] private Material animalSky;
    private float showingTime = 2f;

    [Header("Timeline")]
    [SerializeField] private PlayableDirector ceremonyDirector;

    [Header("dissolve")]
    [SerializeField] private DissolutionCenter dissolutionCenter;

    #endregion

    #region Timeline Signal Methods（无参，供 Signal 调用）

    public void ShowAnimalSky()
    {
        animalSky.DOFloat(1f, "_SilhouetteStrength", showingTime);
    }

    public void ChangeMaterialDodo()
    {
        Debug.Log("ChangeDodoMaterial");
        ChangeAnimalMaterial(AnimalType.Dodo);
    }

    public void ChangeMaterialMoose()           => ChangeAnimalMaterial(AnimalType.Moose);
    public void ChangeMaterialMammoth()         => ChangeAnimalMaterial(AnimalType.Mammoth);
    public void ChangeMaterialZuolong()         => ChangeAnimalMaterial(AnimalType.Zuolong);
    public void ChangeMaterialLittleDragonHunter() => ChangeAnimalMaterial(AnimalType.LittleDragonHunter);
    public void ChangeMaterialLiangLong()       => ChangeAnimalMaterial(AnimalType.LiangLong);
    public void ChangeMaterialTriceratops()     => ChangeAnimalMaterial(AnimalType.Triceratops);
    public void ChangeMaterialPterodactyl()     => ChangeAnimalMaterial(AnimalType.Pterodactyl);

    #endregion

    #region Private

    private void ChangeAnimalMaterial(AnimalType animalType)
    {
        Debug.Log($"[Ceremony] ChangeAnimalMaterial 被调用, animalType={animalType}, 配置数量={animalMaterials.Count}");

        AnimalMaterialEntry entry = animalMaterials.Find(e => e.animalType == animalType);

        if (entry == null)
        {
            Debug.LogWarning($"[Ceremony] 未找到 AnimalType.{animalType} 的材质配置");
            return;
        }

        if (entry.animalObject == null)
        {
            Debug.LogWarning($"[Ceremony] AnimalType.{animalType} 的 animalObject 为空");
            return;
        }

        if (entry.targetMaterials == null || entry.targetMaterials.Length == 0)
        {
            Debug.LogWarning($"[Ceremony] AnimalType.{animalType} 的 targetMaterials 为空");
            return;
        }

        Debug.Log($"[Ceremony] animalObject={entry.animalObject.name}, targetMaterials数量={entry.targetMaterials.Length}");

        Renderer[] renderers = entry.animalObject.GetComponentsInChildren<Renderer>(includeInactive: true);
        Debug.Log($"[Ceremony] 找到 {renderers.Length} 个 Renderer");

        int matIndex = 0;

        foreach (Renderer r in renderers)
        {
            int slotCount = r.sharedMaterials.Length;
            Debug.Log($"[Ceremony] Renderer '{r.name}' 有 {slotCount} 个材质槽");

            if (slotCount <= 0) continue;

            int remaining = entry.targetMaterials.Length - matIndex;
            if (remaining <= 0)
            {
                Debug.LogWarning($"[Ceremony] targetMaterials 不够用，已用完 {entry.targetMaterials.Length} 个");
                break;
            }

            int take = Mathf.Min(slotCount, remaining);
            Material[] newMats = new Material[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                newMats[i] = (i < take) ? entry.targetMaterials[matIndex + i] : r.sharedMaterials[i];
            }

            r.sharedMaterials = newMats;
            matIndex += take;
        }

        Debug.Log($"[Ceremony] 完成：{animalType} 共替换 {matIndex} 个材质槽");
    }

    #endregion
}
