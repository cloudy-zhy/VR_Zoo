using StarlightCollect;
using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// 被召唤出来的半透明灵魂实体行为：
/// - 对着玩家挥手
/// - 显示介绍文本
/// - 自动消失
/// 
/// 由 StarTwinkleInteraction 在召唤序列中实例化并调用。
/// 后续可替换为带骨骼动画的模型，接口保持不变。
/// </summary>
public class SpiritEntity : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("介绍文本的 Canvas / TextMeshPro（如果预制体里有的话）")]
    public TextMeshPro introductionText;
    [Tooltip("名称文本")]
    public TextMeshPro nameText;
    [Tooltip("介绍面板的根 GameObject（用于显隐）")]
    public GameObject introPanel;

    [Header("Visual")]
    [Tooltip("星光粒子（可选）")]
    public ParticleSystem starParticles;
    [Tooltip("实体主体 Renderer")]
    public Renderer bodyRenderer;

    private SpiritSummonConfig config;
    private Transform playerRoot;
    private Material bodyMaterial;
    private Tween waveTween;
    private Tween floatTween;
    private bool isDismissed;

    /// <summary>
    /// 由 StarTwinkleInteraction 调用
    /// </summary>
    public void Initialize(SpiritSummonConfig summonConfig, Transform player)
    {
        config = summonConfig;
        playerRoot = player;

        // 面向玩家
        if (playerRoot != null)
        {
            Vector3 dir = (transform.position - playerRoot.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        // 获取材质引用
        if (bodyRenderer != null)
            bodyMaterial = bodyRenderer.material;

        // 悬浮动画
        StartFloating();

        // 播放粒子
        if (starParticles != null)
            starParticles.Play();
    }

    /// <summary>
    /// 占位 Renderer 模式（由 StarTwinkleInteraction 创建占位球时使用）
    /// </summary>
    public void UsePlaceholderRenderer(Renderer rend)
    {
        bodyRenderer = rend;
        bodyMaterial = rend.material;
    }

    /// <summary>
    /// 播放挥手动画（当前用缩放弹跳模拟，后续可替换为骨骼动画）
    /// </summary>
    public async System.Threading.Tasks.Task PlayWaveAnimation(float duration)
    {
        if (isDismissed || bodyRenderer == null) return;

        // 简单的挥手模拟：缩放弹跳
        Vector3 originalScale = transform.localScale;
        Sequence waveSeq = DOTween.Sequence();

        // 3 次挥手弹跳
        for (int i = 0; i < 3; i++)
        {
            float seg = duration / 3f;
            waveSeq.Append(transform.DOScale(originalScale * 1.15f, seg * 0.3f).SetEase(Ease.OutQuad));
            waveSeq.Append(transform.DOScale(originalScale * 0.9f, seg * 0.3f).SetEase(Ease.InQuad));
            waveSeq.Append(transform.DOScale(originalScale, seg * 0.4f).SetEase(Ease.OutBack));
        }

        waveTween = waveSeq;
        await waveSeq.AsyncWaitForCompletion();
    }

    /// <summary>
    /// 显示介绍文本
    /// </summary>
    public void ShowIntroduction(string intro, string animalName)
    {
        if (isDismissed) return;

        // 创建简易 UI（如果没有预制 UI）
        if (introductionText == null || introPanel == null)
        {
            CreateIntroUI(animalName, intro);
        }

        if (nameText != null)
            nameText.text = animalName;
        if (introductionText != null)
            introductionText.text = intro;
        if (introPanel != null)
        {
            introPanel.SetActive(true);
            introPanel.transform.localScale = Vector3.zero;
            introPanel.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
        }
    }

    /// <summary>
    /// 创建介绍 UI（Canvas + TextMeshPro，始终面向玩家）
    /// </summary>
    private void CreateIntroUI(string title, string body)
    {
        // 创建 Canvas
        GameObject canvasGO = new GameObject($"{name}_IntroCanvas");
        canvasGO.transform.SetParent(transform);
        canvasGO.transform.localPosition = new Vector3(0, 1.5f, 0);
        canvasGO.transform.localScale = Vector3.one * 0.01f; // world space canvas 需要小缩放

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        // 面板背景
        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform);
        panelGO.transform.localPosition = Vector3.zero;
        panelGO.transform.localScale = Vector3.one * 80f;

        UnityEngine.UI.Image panelImage = panelGO.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0f, 0f, 0.1f, 0.8f);

        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(400, 120);

        introPanel = panelGO;
        introPanel.SetActive(false);

        // 名称文本
        GameObject nameGO = new GameObject("NameText");
        nameGO.transform.SetParent(panelGO.transform);
        nameGO.transform.localPosition = new Vector3(0, 30, 0);
        nameGO.transform.localScale = Vector3.one;

        nameText = nameGO.AddComponent<TextMeshPro>();
        nameText.text = title;
        nameText.fontSize = 12;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
        nameText.rectTransform.sizeDelta = new Vector2(380, 30);

        // 介绍文本
        GameObject introGO = new GameObject("IntroText");
        introGO.transform.SetParent(panelGO.transform);
        introGO.transform.localPosition = new Vector3(0, -20, 0);
        introGO.transform.localScale = Vector3.one;

        introductionText = introGO.AddComponent<TextMeshPro>();
        introductionText.text = body;
        introductionText.fontSize = 8;
        introductionText.alignment = TextAlignmentOptions.Center;
        introductionText.color = new Color(0.9f, 0.9f, 1f);
        introductionText.rectTransform.sizeDelta = new Vector2(380, 60);

        // 让 Canvas 始终面向玩家
        AlwaysFacePlayer faceScript = canvasGO.AddComponent<AlwaysFacePlayer>();
        faceScript.playerTransform = playerRoot;
    }

    /// <summary>
    /// 优雅消失
    /// </summary>
    public async void Dismiss()
    {
        if (isDismissed) return;
        isDismissed = true;

        waveTween?.Kill();
        floatTween?.Kill();

        if (introPanel != null)
            introPanel.transform.DOScale(0f, 0.3f).SetEase(Ease.InBack);

        if (starParticles != null)
            starParticles.Stop();

        await transform.DOScale(0f, 0.5f)
            .SetEase(Ease.InBack)
            .AsyncWaitForCompletion();

        Destroy(gameObject);
    }

    void StartFloating()
    {
        floatTween = transform.DOMoveY(transform.position.y + 0.3f, 2f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    void OnDestroy()
    {
        waveTween?.Kill();
        floatTween?.Kill();
    }
}

/// <summary>
/// 辅助组件：让 Canvas 始终面向玩家
/// </summary>
public class AlwaysFacePlayer : MonoBehaviour
{
    public Transform playerTransform;

    void LateUpdate()
    {
        if (playerTransform == null)
        {
            var cam = Camera.main;
            if (cam != null) playerTransform = cam.transform;
        }
        if (playerTransform == null) return;

        Vector3 dir = transform.position - playerTransform.position;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}