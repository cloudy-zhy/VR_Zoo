using StarlightCollect;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene4 祭坛星空的 Manager。
/// - 管理所有 StarTwinkleInteraction 的激活时机
/// - 接收召唤事件
/// - 提供已召唤动物的记录（后续用于图鉴等）
/// 
/// 挂载到场景中的任意 GameObject。
/// </summary>
public class CeremonyStarManager : MonoBehaviour
{
    [Header("Stars")]
    [Tooltip("场景中所有可交互的星星（拖入或自动查找）")]
    public List<StarTwinkleInteraction> starInteractions;

    [Header("Settings")]
    [Tooltip("Timeline 播放后延迟多少秒激活星星")]
    public float activationDelay = 0f;
    [Tooltip("星星逐个激活的时间间隔")]
    public float staggerInterval = 0.3f;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent<SpiritSummonConfig> onAnySpiritSummoned;

    /// <summary>
    /// 已召唤的动物名称列表（用于后续扩展）
    /// </summary>
    public List<string> SummonedAnimals { get; private set; } = new List<string>();

    private bool hasActivated;

    void Start()
    {
        // 星星由 CeremonyEffectsController 动态生成，Start 时还不存在
        // 延迟到 ActivateStars 时刷新
    }

    /// <summary>
    /// 刷新星星列表（在 CeremonyEffectsController 生成星星后调用）
    /// </summary>
    public void RefreshStars()
    {
        var found = FindObjectsOfType<StarTwinkleInteraction>();
        if (found != null && found.Length > 0)
        {
            starInteractions = new List<StarTwinkleInteraction>(found);
            foreach (var star in starInteractions)
            {
                if (star != null)
                    star.onStarSelected.AddListener(OnStarSelected);
            }
        }
    }

    /// <summary>
    /// 由 Timeline Signal 或外部调用，激活星空交互
    /// </summary>
    [ContextMenu("Activate All Stars")]
    public void ActivateStars()
    {
        if (hasActivated) return;
        hasActivated = true;

        StartCoroutine(ActivateStarsRoutine());
    }

    private System.Collections.IEnumerator ActivateStarsRoutine()
    {
        yield return new WaitForSeconds(activationDelay);

        foreach (var star in starInteractions)
        {
            if (star == null) continue;

            // 显示星星
            if (star.mainRenderer != null)
            {
                star.mainRenderer.enabled = true;
                // 重新启动闪烁
                var twinkle = star.GetComponent<StarTwinkle>();
                if (twinkle != null)
                    twinkle.StartTwinkle();
            }

            yield return new WaitForSeconds(staggerInterval);
        }
    }

    /// <summary>
    /// 当任意星星被选择时
    /// </summary>
    private void OnStarSelected(SpiritSummonConfig config)
    {
        if (config != null && !SummonedAnimals.Contains(config.animalName))
            SummonedAnimals.Add(config.animalName);

        onAnySpiritSummoned?.Invoke(config);

        Debug.Log($"[CeremonyStarManager] 召唤了 {config?.animalName ?? "???"}");
    }

    /// <summary>
    /// 检查某动物是否已被召唤
    /// </summary>
    public bool HasSummoned(string animalName)
    {
        return SummonedAnimals.Contains(animalName);
    }

    void OnDestroy()
    {
        foreach (var star in starInteractions)
            if (star != null)
                star.onStarSelected.RemoveListener(OnStarSelected);
    }
}