using StarlightCollect;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using DG.Tweening;
using Cysharp.Threading.Tasks;

/// <summary>
/// 挂载在 CeremonyStar 上，替换纯 StarTwinkle。
/// 当玩家用手柄抓取（或将来手势识别选择）这颗星时，
/// 触发动物剪影坠落 → 流星 → 灵魂实体的完整召唤序列。
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class StarTwinkleInteraction : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("此星星对应的动物配置")]
    public SpiritSummonConfig summonConfig;

    [Header("References")]
    [Tooltip("星星的 Renderer（用于闪烁和隐藏）")]
    public Renderer starRenderer;
    [Tooltip("玩家根Transform（XR Origin），用于计算落地位置")]
    public Transform playerRoot;

    [Header("Events")]
    [Tooltip("当星星被选择时触发")]
    public UnityEngine.Events.UnityEvent<SpiritSummonConfig> onStarSelected;
    [Tooltip("当灵魂实体完全出现后触发")]
    public UnityEngine.Events.UnityEvent<SpiritSummonConfig> onSpiritAppeared;

    private XRGrabInteractable grabInteractable;
    private Material starMaterial;
    private bool isSelected;
    private SpiritEntity activeSpirit;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (starRenderer != null)
            starMaterial = starRenderer.material;
    }

    void Start()
    {
        // 抓取事件
        grabInteractable.selectEntered.AddListener(OnGrabbed);

        // 如果没拖 playerRoot，尝试自动查找
        if (playerRoot == null)
        {
            // 按名称找 XR Origin，避免命名空间问题
            var xrGO = GameObject.Find("XR Origin");
            if (xrGO != null) playerRoot = xrGO.transform;
        }

        // 如果没有 starRenderer，尝试从自身获取
        if (starRenderer == null)
            starRenderer = GetComponent<Renderer>();
        if (starMaterial == null && starRenderer != null)
            starMaterial = starRenderer.material;
    }

    void OnDestroy()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
    }

    /// <summary>
    /// 玩家抓取了这颗星星
    /// </summary>
    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (isSelected) return;
        isSelected = true;

        // 立即松开（不需要持续抓着）
        if (args.interactorObject is XRBaseInteractor interactor)
            grabInteractable.interactionManager.SelectExit(
                (IXRSelectInteractor)interactor,
                (IXRSelectInteractable)grabInteractable);

        // 触发选择事件
        onStarSelected?.Invoke(summonConfig);

        // 开始召唤序列
        SummonSpirit();
    }

    /// <summary>
    /// 核心召唤序列：
    /// 1. 星星闪烁并消失
    /// 2. 在星星位置生成影像 → 坠落
    /// 3. 影像化作流星飞向玩家
    /// 4. 在玩家身边聚合成灵魂实体
    /// 5. 实体挥手 + 显示介绍
    /// </summary>
    private async void SummonSpirit()
    {
        if (summonConfig == null)
        {
            Debug.LogWarning($"[StarTwinkleInteraction] {name} 没有配置 SpiritSummonConfig");
            isSelected = false;
            return;
        }

        // ---- Phase 1: 星星闪烁消失 ----
        if (starMaterial != null)
        {
            // URP shader uses _BaseColor, not _Color; use DOColor instead
            Color baseC = starMaterial.GetColor("_BaseColor");
            await starMaterial.DOColor(new Color(baseC.r, baseC.g, baseC.b, 0f), 0.4f)
                .SetEase(Ease.InQuad)
                .AsyncWaitForCompletion();
        }
        if (starRenderer != null)
            starRenderer.enabled = false;

        // ---- Phase 2: 生成影像，从星空坠落 ----
        Vector3 starPosition = transform.position;
        Vector3 playerPosition = playerRoot != null ? playerRoot.position : Camera.main.transform.position;
        Vector3 landPosition = playerPosition + (playerRoot != null ? playerRoot.forward : Vector3.forward) * summonConfig.landOffset.z;
        landPosition += Vector3.up * summonConfig.landOffset.y + (playerRoot != null ? playerRoot.right : Vector3.right) * summonConfig.landOffset.x;

        // 生成临时坠落影像（用星星本身或一个简单球体）
        GameObject fallingSprite = CreateFallingSprite(starPosition);
        if (fallingSprite != null)
        {
            // 弧线坠落
            Vector3 midPoint = (starPosition + landPosition) * 0.5f + Vector3.up * summonConfig.arcHeight;

            Sequence fallSeq = DOTween.Sequence();
            fallSeq.Append(fallingSprite.transform.DOMove(midPoint, summonConfig.fallDuration * 0.5f).SetEase(Ease.OutQuad));
            fallSeq.Join(fallingSprite.transform.DOScale(transform.localScale * 0.7f, summonConfig.fallDuration * 0.5f));
            fallSeq.Append(fallingSprite.transform.DOMove(landPosition, summonConfig.fallDuration * 0.5f).SetEase(Ease.InQuad));
            await fallSeq.AsyncWaitForCompletion();
            Vector3 groundPos = landPosition - Vector3.up * 0.2f;
            fallingSprite.transform.DOScale(0.3f, summonConfig.meteorDuration).SetEase(Ease.InBack);
            await fallingSprite.transform.DOMove(groundPos, summonConfig.meteorDuration)
                .SetEase(Ease.InFlash)
                .AsyncWaitForCompletion();

            Destroy(fallingSprite);
        }

        // ---- Phase 4: 灵魂实体聚合 ----
        Vector3 spiritPosition = landPosition;
        if (summonConfig.spiritPrefab != null)
        {
            GameObject spiritGO = Instantiate(summonConfig.spiritPrefab, spiritPosition, Quaternion.identity);
            activeSpirit = spiritGO.GetComponent<SpiritEntity>();
            if (activeSpirit == null)
                activeSpirit = spiritGO.AddComponent<SpiritEntity>();

            activeSpirit.Initialize(summonConfig, playerRoot);
            spiritGO.transform.localScale = Vector3.zero;

            // 从无到有的聚合缩放
            await spiritGO.transform.DOScale(summonConfig.spiritScale, summonConfig.assembleDuration)
                .SetEase(Ease.OutBack)
                .AsyncWaitForCompletion();
        }
        else
        {
            // 没有预制体：创建一个占位的半透明球
            activeSpirit = CreatePlaceholderSpirit(spiritPosition);
            if (activeSpirit != null)
                activeSpirit.Initialize(summonConfig, playerRoot);
        }

        // ---- Phase 5: 挥手 + 显示介绍 ----
        if (activeSpirit != null)
        {
            await activeSpirit.PlayWaveAnimation(summonConfig.waveDuration);
            activeSpirit.ShowIntroduction(summonConfig.introductionText, summonConfig.animalName);
        }

        onSpiritAppeared?.Invoke(summonConfig);

        // ---- 自动清理（如果配置了存活时间）----
        if (summonConfig.spiritLifetime > 0 && activeSpirit != null)
        {
            await UniTask.Delay((int)(summonConfig.spiritLifetime * 1000));
            if (activeSpirit != null)
                activeSpirit.Dismiss();
        }

        isSelected = false;
    }

    /// <summary>
    /// 生成坠落的影像（用星星副本）—— 后续可替换为更精美的效果
    /// </summary>
    private GameObject CreateFallingSprite(Vector3 position)
    {
        if (starRenderer == null) return null;

        GameObject sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sprite.name = $"{name}_FallingSprite";
        sprite.transform.position = position;
        sprite.transform.rotation = Quaternion.LookRotation(Vector3.down);
        sprite.transform.localScale = transform.localScale * 0.5f;

        // 复用星星材质
        var rend = sprite.GetComponent<Renderer>();
        if (rend != null && starMaterial != null)
        {
            Material copyMat = new Material(starMaterial);
            copyMat.SetFloat("_Surface", 1); // 透明
            rend.material = copyMat;
        }

        Destroy(sprite.GetComponent<Collider>());
        return sprite;
    }

    /// <summary>
    /// 创建一个占位灵魂实体（没有 spiritPrefab 时的降级方案）
    /// </summary>
    private SpiritEntity CreatePlaceholderSpirit(Vector3 position)
    {
        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        placeholder.name = $"{summonConfig.animalName}_SpiritPlaceholder";
        placeholder.transform.position = position;
        placeholder.transform.localScale = Vector3.zero;

        var rend = placeholder.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(1f, 0.9f, 0.6f, 0.5f);
            // 半透明设置
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            rend.material = mat;
        }

        Destroy(placeholder.GetComponent<Collider>());

        var spirit = placeholder.AddComponent<SpiritEntity>();
        spirit.UsePlaceholderRenderer(rend);
        return spirit;
    }
}
