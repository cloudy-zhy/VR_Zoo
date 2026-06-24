using StarlightCollect;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using DG.Tweening;
using Cysharp.Threading.Tasks;

/// <summary>
/// 挂载在祭坛上空的动物剪影物体上。
/// 在星空中：Shadow 材质 + 闪烁 + 绕祭坛旋转。
/// 被玩家抓取后：自身坠落 → 切换 Normal 材质 → 显示介绍 → 自动消失。
/// 
/// 物体需预先放在场景中（失活），由 CeremonyEffectsController 激活并定位。
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class StarTwinkleInteraction : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("此动物的召唤配置")]
    public SpiritSummonConfig summonConfig;

    [Header("Materials")]
    [Tooltip("在星空中使用的剪影材质")]
    public Material shadowMaterial;
    [Tooltip("召唤后使用的正常材质")]
    public Material normalMaterial;

    [Header("Sky Behavior")]
    [Tooltip("绕祭坛旋转的速度")]
    public float orbitSpeed = 10f;
    [Tooltip("绕祭坛旋转的半径偏移")]
    public float orbitRadiusOffset = 0f;

    [Header("Summon Animation")]
    [Tooltip("落到玩家身边的时长")]
    public float fallDuration = 1.5f;
    [Tooltip("聚合缩放时长")]
    public float assembleDuration = 0.6f;
    [Tooltip("落地偏移")]
    public Vector3 landOffset = new Vector3(0f, 0.5f, 2f);
    [Tooltip("挥手时长")]
    public float waveDuration = 1.5f;
    [Tooltip("存活时间（秒，-1 永久）")]
    public float lifetime = 20f;

    [Header("References")]
    public Renderer mainRenderer;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent<SpiritSummonConfig> onStarSelected;
    public UnityEngine.Events.UnityEvent<SpiritSummonConfig> onSpiritAppeared;

    // Internal
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Transform orbitCenter;
    private bool isInSky = true;
    private bool isSelected;
    private Material instanceShadowMat;
    private Material instanceNormalMat;
    private Vector3 savedSkyScale;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        if (mainRenderer == null)
            mainRenderer = GetComponent<Renderer>();

        // 实例化材质（避免共享材质问题）
        if (shadowMaterial != null)
            instanceShadowMat = new Material(shadowMaterial);
        if (normalMaterial != null)
            instanceNormalMat = new Material(normalMaterial);

        // 初始用 Shadow 材质
        ApplyMaterial(instanceShadowMat);
    }

    void Start()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
    }

    void Update()
    {
        // 星空中绕祭坛旋转
        if (isInSky && orbitCenter != null)
        {
            Vector3 dir = transform.position - orbitCenter.position;
            float radius = dir.magnitude;
            float angle = orbitSpeed * Time.deltaTime;
            // 绕 Y 轴旋转
            Vector3 rotated = Quaternion.AngleAxis(angle, Vector3.up) * dir;
            transform.position = orbitCenter.position + rotated.normalized * (radius + orbitRadiusOffset);
            
            // 始终面向祭坛中心
            Vector3 lookDir = (orbitCenter.position - transform.position).normalized;
            if (lookDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }

    void OnDestroy()
    {
        grabInteractable?.selectEntered.RemoveListener(OnGrabbed);
        if (instanceShadowMat != null) Destroy(instanceShadowMat);
        if (instanceNormalMat != null) Destroy(instanceNormalMat);
    }

    /// <summary>
    /// 由 CeremonyEffectsController 调用：放入星空
    /// </summary>
    public void PlaceInSky(Transform center, Vector3 position)
    {
        orbitCenter = center;
        transform.position = position;
        savedSkyScale = transform.localScale;
        isInSky = true;
        isSelected = false;

        ApplyMaterial(instanceShadowMat);
        gameObject.SetActive(true);

        // 确保碰撞体和交互就绪
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
        grabInteractable.enabled = true;
    }

    /// <summary>
    /// 玩家抓取了这颗星
    /// </summary>
    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (isSelected || !isInSky) return;
        isSelected = true;
        isInSky = false;

        // 立即松开
        if (args.interactorObject is XRBaseInteractor interactor)
            grabInteractable.interactionManager.SelectExit(
                (IXRSelectInteractor)interactor,
                (IXRSelectInteractable)grabInteractable);

        onStarSelected?.Invoke(summonConfig);

        // 开始召唤序列
        SummonToPlayer();
    }

    /// <summary>
    /// 从星空坠落到玩家身边，切换材质，显示介绍
    /// </summary>
    private async void SummonToPlayer()
    {
        if (summonConfig == null)
        {
            Debug.LogWarning($"[StarTwinkleInteraction] {name} 无 SpiritSummonConfig");
            isSelected = false;
            return;
        }

        // 禁用抓取
        grabInteractable.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // ---- Phase 1: 弧线坠落到玩家身边 ----
        Transform playerRoot = FindPlayerRoot();
        Vector3 starPos = transform.position;
        Vector3 playerPos = playerRoot != null ? playerRoot.position : Camera.main.transform.position;
        Vector3 landPos = playerPos
            + (playerRoot != null ? playerRoot.forward : Vector3.forward) * landOffset.z
            + Vector3.up * landOffset.y
            + (playerRoot != null ? playerRoot.right : Vector3.right) * landOffset.x;

        Vector3 midPoint = (starPos + landPos) * 0.5f + Vector3.up * summonConfig.arcHeight;

        Sequence fallSeq = DOTween.Sequence();
        fallSeq.Append(transform.DOMove(midPoint, fallDuration * 0.5f).SetEase(Ease.OutQuad));
        fallSeq.Join(transform.DOScale(savedSkyScale * 0.6f, fallDuration * 0.5f));
        fallSeq.Append(transform.DOMove(landPos, fallDuration * 0.5f).SetEase(Ease.InQuad));
        await fallSeq.AsyncWaitForCompletion();

        // ---- Phase 2: 切换材质 + 聚合缩放 ----
        ApplyMaterial(instanceNormalMat);
        transform.localScale = Vector3.zero;

        await transform.DOScale(savedSkyScale, assembleDuration)
            .SetEase(Ease.OutBack)
            .AsyncWaitForCompletion();

        // ---- Phase 3: 面向玩家挥手 ----
        if (playerRoot != null)
        {
            Vector3 faceDir = (transform.position - playerRoot.position).normalized;
            faceDir.y = 0;
            if (faceDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(faceDir);
        }

        await PlayWaveAnimation(waveDuration);

        // ---- Phase 4: 显示介绍 UI ----
        ShowIntroductionUI();

        onSpiritAppeared?.Invoke(summonConfig);

        // ---- Phase 5: 自动消失 ----
        if (lifetime > 0)
        {
            await UniTask.Delay((int)(lifetime * 1000));
            await Dismiss();
        }

        isSelected = false;
    }

    /// <summary>
    /// 挥手动画（缩放弹跳，后续可替换骨骼动画）
    /// </summary>
    private async UniTask PlayWaveAnimation(float duration)
    {
        Vector3 orig = transform.localScale;
        Sequence seq = DOTween.Sequence();
        for (int i = 0; i < 3; i++)
        {
            float seg = duration / 3f;
            seq.Append(transform.DOScale(orig * 1.15f, seg * 0.3f).SetEase(Ease.OutQuad));
            seq.Append(transform.DOScale(orig * 0.9f, seg * 0.3f).SetEase(Ease.InQuad));
            seq.Append(transform.DOScale(orig, seg * 0.4f).SetEase(Ease.OutBack));
        }
        await seq.AsyncWaitForCompletion();
    }

    /// <summary>
    /// 显示介绍 UI（始终面向玩家）
    /// </summary>
    private void ShowIntroductionUI()
    {
        if (summonConfig == null) return;

        // 创建 Canvas
        GameObject canvasGO = new GameObject($"{summonConfig.animalName}_Intro");
        canvasGO.transform.SetParent(transform);
        canvasGO.transform.localPosition = new Vector3(0, 1.8f, 0);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvasGO.transform.localScale = Vector3.one * 0.008f;

        // 面板
        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform);
        panelGO.transform.localPosition = Vector3.zero;
        panelGO.transform.localScale = Vector3.one * 80f;
        var img = panelGO.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0f, 0f, 0.1f, 0.85f);
        panelGO.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 100);

        // 名称
        GameObject nameGO = new GameObject("Name");
        nameGO.transform.SetParent(panelGO.transform);
        nameGO.transform.localPosition = new Vector3(0, 25, 0);
        var nameTmp = nameGO.AddComponent<TMPro.TextMeshPro>();
        nameTmp.text = summonConfig.animalName;
        nameTmp.fontSize = 11;
        nameTmp.alignment = TMPro.TextAlignmentOptions.Center;
        nameTmp.color = Color.white;
        nameTmp.rectTransform.sizeDelta = new Vector2(380, 30);

        // 介绍
        GameObject introGO = new GameObject("Intro");
        introGO.transform.SetParent(panelGO.transform);
        introGO.transform.localPosition = new Vector3(0, -20, 0);
        var introTmp = introGO.AddComponent<TMPro.TextMeshPro>();
        introTmp.text = summonConfig.introductionText;
        introTmp.fontSize = 7;
        introTmp.alignment = TMPro.TextAlignmentOptions.Center;
        introTmp.color = new Color(0.9f, 0.9f, 1f);
        introTmp.rectTransform.sizeDelta = new Vector2(380, 55);

        // 面向玩家
        var face = canvasGO.AddComponent<AlwaysFacePlayer>();
        face.playerTransform = FindPlayerRoot();

        // 面板入场动画
        panelGO.transform.localScale = Vector3.zero;
        panelGO.transform.DOScale(Vector3.one * 80f, 0.4f).SetEase(Ease.OutBack);
    }

    private async UniTask Dismiss()
    {
        await transform.DOScale(0f, 0.5f).SetEase(Ease.InBack).AsyncWaitForCompletion();
        gameObject.SetActive(false);
    }

    private void ApplyMaterial(Material mat)
    {
        if (mainRenderer != null && mat != null)
            mainRenderer.material = mat;
    }

    private Transform FindPlayerRoot()
    {
        var xrGO = GameObject.Find("XR Origin");
        if (xrGO != null) return xrGO.transform;
        if (Camera.main != null && Camera.main.transform.parent != null)
            return Camera.main.transform.parent;
        return Camera.main?.transform;
    }
}