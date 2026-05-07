using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("对话框引用")]
    [SerializeField] private GameObject dialogBoxSelf; // 玩家对话框
    [SerializeField] private GameObject dialogBoxOthers; // NPC对话框
    [SerializeField] private Text selfText; // 玩家文本
    [SerializeField] private Text othersText; // NPC文本

    [Header("玩家对话框设置")]
    [SerializeField] private Image selfPortrait; // 玩家头像
    [SerializeField] private Text selfNameText; // 玩家名称

    [Header("NPC对话框设置")]
    [SerializeField] private Image othersPortrait; // NPC头像
    [SerializeField] private Text othersNameText; // NPC名称

    [Header("分数和目标")]
    [SerializeField] private GameObject scoreAimPanel; // 分数和目标面板
    [SerializeField] private Text scoreText; // 分数文本
    [SerializeField] private Text aimText; // 目标文本
    [SerializeField] private Text highScoreText; // 最高分文本（可选）

    [Header("动画设置")]
    [SerializeField] private float dialogFadeSpeed = 5f; // 对话框淡入淡出速度
    [SerializeField] private float typewriterSpeed = 30f; // 打字机效果速度（字符/秒）

    [Header("UI状态")]
    [SerializeField] private bool isDialogActive = false;
    [SerializeField] private bool isTyping = false;
    
    [Header("初始时分数的显示与否（暂时）")]
    [SerializeField] private bool isInitialShowScore = false;

    // 私有变量
    private CanvasGroup selfDialogCanvasGroup;
    private CanvasGroup othersDialogCanvasGroup;
    private Coroutine currentTypingCoroutine;

    // 事件
    public static event Action OnDialogStarted;
    public static event Action OnDialogEnded;
    public static event Action OnTypingComplete;

    // 单例模式（可选）
    public static UIManager Instance { get; private set; }

    // 分数相关
    private int currentScore = 0;
    private int highScore = 0;
    private int targetScore = 0;
    private string currentAim = "";

    #region Unity生命周期方法

    private void Awake()
    {
        Application.targetFrameRate = 72;
        // 单例设置
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 初始化组件
        InitializeComponents();
    }

    private void Start()
    {
        // 初始隐藏对话框
        HideAllDialogs();

        // 显示分数面板
        ShowScoreAimPanel(isInitialShowScore);

        // 初始化分数显示
        UpdateScoreDisplay();
    }

    private void Update()
    {
        // 检测点击跳过打字机效果
        if (isTyping && Input.GetMouseButtonDown(0))
        {
            SkipTyping();
        }
    }

    #endregion

    #region 初始化方法

    private void InitializeComponents()
    {
        // 获取CanvasGroup组件，用于淡入淡出效果
        if (dialogBoxSelf != null)
        {
            selfDialogCanvasGroup = dialogBoxSelf.GetComponent<CanvasGroup>();
            if (selfDialogCanvasGroup == null)
            {
                selfDialogCanvasGroup = dialogBoxSelf.AddComponent<CanvasGroup>();
            }
        }

        if (dialogBoxOthers != null)
        {
            othersDialogCanvasGroup = dialogBoxOthers.GetComponent<CanvasGroup>();
            if (othersDialogCanvasGroup == null)
            {
                othersDialogCanvasGroup = dialogBoxOthers.AddComponent<CanvasGroup>();
            }
        }
    }

    #endregion

    #region 对话框控制方法

    /// <summary>
    /// 显示玩家对话框
    /// </summary>
    public void ShowSelfDialog(string message, string speakerName = "玩家", Sprite portrait = null)
    {
        if (dialogBoxSelf == null || selfText == null) return;

        HideAllDialogs();
        isDialogActive = true;

        // 设置说话者名称
        if (selfNameText != null)
        {
            selfNameText.text = speakerName;
        }

        // 设置头像
        if (selfPortrait != null)
        {
            if (portrait != null)
            {
                selfPortrait.sprite = portrait;
                selfPortrait.gameObject.SetActive(true);
            }
            else
            {
                selfPortrait.gameObject.SetActive(false);
            }
        }

        // 显示并淡入
        dialogBoxSelf.SetActive(true);
        StartCoroutine(FadeInDialog(selfDialogCanvasGroup));

        // 触发打字机效果
        if (currentTypingCoroutine != null)
            StopCoroutine(currentTypingCoroutine);

        currentTypingCoroutine = StartCoroutine(TypewriterEffect(message, selfText));

        OnDialogStarted?.Invoke();
    }

    /// <summary>
    /// 显示NPC对话框
    /// </summary>
    public void ShowOthersDialog(string message, string speakerName = "NPC", Sprite portrait = null)
    {
        if (dialogBoxOthers == null || othersText == null) return;

        HideAllDialogs();
        isDialogActive = true;

        // 设置说话者名称
        if (othersNameText != null)
        {
            othersNameText.text = speakerName;
        }

        // 设置头像
        if (othersPortrait != null)
        {
            if (portrait != null)
            {
                othersPortrait.sprite = portrait;
                othersPortrait.gameObject.SetActive(true);
            }
            else
            {
                othersPortrait.gameObject.SetActive(false);
            }
        }

        // 显示并淡入
        dialogBoxOthers.SetActive(true);
        StartCoroutine(FadeInDialog(othersDialogCanvasGroup));

        // 触发打字机效果
        if (currentTypingCoroutine != null)
            StopCoroutine(currentTypingCoroutine);

        currentTypingCoroutine = StartCoroutine(TypewriterEffect(message, othersText));

        OnDialogStarted?.Invoke();
    }

    /// <summary>
    /// 隐藏所有对话框
    /// </summary>
    public void HideAllDialogs()
    {
        if (currentTypingCoroutine != null)
        {
            StopCoroutine(currentTypingCoroutine);
            isTyping = false;
        }

        if (dialogBoxSelf != null && dialogBoxSelf.activeSelf)
        {
            StartCoroutine(FadeOutDialog(selfDialogCanvasGroup, () => dialogBoxSelf.SetActive(false)));
        }

        if (dialogBoxOthers != null && dialogBoxOthers.activeSelf)
        {
            StartCoroutine(FadeOutDialog(othersDialogCanvasGroup, () => dialogBoxOthers.SetActive(false)));
        }

        isDialogActive = false;
        OnDialogEnded?.Invoke();
    }

    /// <summary>
    /// 跳过打字机效果，立即显示完整文本
    /// </summary>
    public void SkipTyping()
    {
        if (currentTypingCoroutine != null)
        {
            StopCoroutine(currentTypingCoroutine);
            isTyping = false;

            OnTypingComplete?.Invoke();
        }
    }

    /// <summary>
    /// 快速显示玩家对话框（无打字机效果）
    /// </summary>
    public void ShowSelfDialogInstant(string message, string speakerName = "玩家", Sprite portrait = null)
    {
        if (dialogBoxSelf == null || selfText == null) return;

        HideAllDialogs();
        isDialogActive = true;

        // 设置说话者名称
        if (selfNameText != null)
        {
            selfNameText.text = speakerName;
        }

        // 设置头像
        if (selfPortrait != null)
        {
            if (portrait != null)
            {
                selfPortrait.sprite = portrait;
                selfPortrait.gameObject.SetActive(true);
            }
            else
            {
                selfPortrait.gameObject.SetActive(false);
            }
        }

        // 直接显示文本
        selfText.text = message;

        // 显示并淡入
        dialogBoxSelf.SetActive(true);
        if (selfDialogCanvasGroup != null)
        {
            selfDialogCanvasGroup.alpha = 1f;
        }

        OnDialogStarted?.Invoke();
    }

    /// <summary>
    /// 快速显示NPC对话框（无打字机效果）
    /// </summary>
    public void ShowOthersDialogInstant(string message, string speakerName = "NPC", Sprite portrait = null)
    {
        if (dialogBoxOthers == null || othersText == null) return;

        HideAllDialogs();
        isDialogActive = true;

        // 设置说话者名称
        if (othersNameText != null)
        {
            othersNameText.text = speakerName;
        }

        // 设置头像
        if (othersPortrait != null)
        {
            if (portrait != null)
            {
                othersPortrait.sprite = portrait;
                othersPortrait.gameObject.SetActive(true);
            }
            else
            {
                othersPortrait.gameObject.SetActive(false);
            }
        }

        // 直接显示文本
        othersText.text = message;

        // 显示并淡入
        dialogBoxOthers.SetActive(true);
        if (othersDialogCanvasGroup != null)
        {
            othersDialogCanvasGroup.alpha = 1f;
        }

        OnDialogStarted?.Invoke();
    }

    /// <summary>
    /// 检查对话框是否激活
    /// </summary>
    public bool IsDialogActive()
    {
        return isDialogActive;
    }

    #endregion

    #region 分数系统方法

    /// <summary>
    /// 设置当前分数
    /// </summary>
    public void SetScore(int score)
    {
        currentScore = score;

        // 更新最高分
        if (currentScore > highScore)
        {
            highScore = currentScore;
        }

        UpdateScoreDisplay();
    }

    /// <summary>
    /// 添加分数
    /// </summary>
    public void AddScore(int points)
    {
        currentScore += points;

        // 更新最高分
        if (currentScore > highScore)
        {
            highScore = currentScore;
        }

        UpdateScoreDisplay();

        // 可以在这里添加分数增加的动画效果
        StartCoroutine(ScorePopupAnimation(points));
    }

    /// <summary>
    /// 设置目标分数
    /// </summary>
    public void SetTargetScore(int target)
    {
        targetScore = target;
        UpdateScoreDisplay();
    }

    /// <summary>
    /// 设置目标文本
    /// </summary>
    public void SetAimText(string aim)
    {
        currentAim = aim;
        if (aimText != null)
        {
            aimText.text = aim;
        }
    }

    /// <summary>
    /// 显示/隐藏分数面板
    /// </summary>
    public void ShowScoreAimPanel(bool show)
    {
        if (scoreAimPanel != null)
        {
            scoreAimPanel.SetActive(show);
        }
    }

    /// <summary>
    /// 重置分数
    /// </summary>
    public void ResetScore()
    {
        currentScore = 0;
        UpdateScoreDisplay();
    }

    /// <summary>
    /// 重置最高分
    /// </summary>
    public void ResetHighScore()
    {
        highScore = 0;
        UpdateScoreDisplay();
    }

    /// <summary>
    /// 检查是否达到目标分数
    /// </summary>
    public bool IsTargetReached()
    {
        return currentScore >= targetScore;
    }

    #endregion

    #region 辅助方法

    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = $"分数: {currentScore}";
        }

        if (highScoreText != null)
        {
            highScoreText.text = $"最高分: {highScore}";
        }
    }

    #endregion

    #region 协程方法

    /// <summary>
    /// 打字机效果
    /// </summary>
    private IEnumerator TypewriterEffect(string message, Text textComponent)
    {
        if (textComponent == null) yield break;

        isTyping = true;
        string currentText = "";
        textComponent.text = "";

        for (int i = 0; i < message.Length; i++)
        {
            currentText += message[i];
            textComponent.text = currentText;

            // 根据速度等待
            yield return new WaitForSeconds(1f / typewriterSpeed);
        }

        isTyping = false;
        OnTypingComplete?.Invoke();
    }

    /// <summary>
    /// 淡入对话框
    /// </summary>
    private IEnumerator FadeInDialog(CanvasGroup canvasGroup)
    {
        if (canvasGroup == null) yield break;

        canvasGroup.alpha = 0;
        float targetAlpha = 1f;

        while (canvasGroup.alpha < targetAlpha)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, dialogFadeSpeed * Time.deltaTime);
            yield return null;
        }
    }

    /// <summary>
    /// 淡出对话框
    /// </summary>
    private IEnumerator FadeOutDialog(CanvasGroup canvasGroup, Action onComplete = null)
    {
        if (canvasGroup == null) yield break;

        float startAlpha = canvasGroup.alpha;

        while (canvasGroup.alpha > 0)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0, dialogFadeSpeed * Time.deltaTime);
            yield return null;
        }

        onComplete?.Invoke();
    }

    /// <summary>
    /// 分数弹出动画
    /// </summary>
    private IEnumerator ScorePopupAnimation(int points)
    {
        // 这里可以添加分数变化的动画效果
        // 例如：创建一个临时文本显示"+100"并向上移动消失

        // 这只是示例，你可以根据需要实现
        yield return null;
    }

    #endregion

    #region 公共属性

    public int CurrentScore
    {
        get { return currentScore; }
    }

    public int HighScore
    {
        get { return highScore; }
    }

    public int TargetScore
    {
        get { return targetScore; }
    }

    public string CurrentAim
    {
        get { return currentAim; }
    }

    public bool IsTyping
    {
        get { return isTyping; }
    }

    #endregion

    #region 编辑器方法

    // 在编辑器中快速测试
    [ContextMenu("测试玩家对话框")]
    private void TestSelfDialog()
    {
        ShowSelfDialog("你好，我是玩家！这是一条测试消息。", "测试玩家");
    }

    [ContextMenu("测试NPC对话框")]
    private void TestOthersDialog()
    {
        ShowOthersDialog("你好，旅行者！欢迎来到我们的世界。", "测试NPC");
    }

    [ContextMenu("测试分数系统")]
    private void TestScoreSystem()
    {
        AddScore(100);
        SetAimText("新目标：找到隐藏的宝藏");
    }

    [ContextMenu("隐藏所有对话框")]
    private void TestHideDialogs()
    {
        HideAllDialogs();
    }

    [ContextMenu("测试快速对话框")]
    private void TestInstantDialog()
    {
        ShowSelfDialogInstant("这是一条立即显示的测试消息。", "快速测试");
    }

    #endregion
}