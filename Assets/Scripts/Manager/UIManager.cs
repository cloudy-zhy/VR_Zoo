using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("对话框引用")]
    [SerializeField] private GameObject dialogBoxSelf; // 玩家对话框
    [SerializeField] private Text selfText; // 玩家文本
    [SerializeField] private GameObject dialogBoxOthers; // NPC对话框
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

    // 单例模式
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
        Debug.Log("UIManager Awake 执行了");
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("UIManager 赋值单例成功");
        }
        else
        {
            Debug.Log("发现重复UIManager，销毁自身");
            Destroy(gameObject);
            return;
        }
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
        // 获取CanvasGroup组件
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
    /// 显示玩家对话框（无淡入，直接弹出）
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

        // 直接显示，移除淡入逻辑
        dialogBoxSelf.SetActive(true);
        selfDialogCanvasGroup.alpha = 1f;

        // 触发打字机效果
        if (currentTypingCoroutine != null)
            StopCoroutine(currentTypingCoroutine);

        currentTypingCoroutine = StartCoroutine(TypewriterEffect(message, selfText));

        OnDialogStarted?.Invoke();
    }

    /// <summary>
    /// 显示NPC对话框（无淡入，直接弹出）
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

        // 直接显示，移除淡入逻辑
        dialogBoxOthers.SetActive(true);
        othersDialogCanvasGroup.alpha = 1f;

        // 触发打字机效果
        if (currentTypingCoroutine != null)
            StopCoroutine(currentTypingCoroutine);

        currentTypingCoroutine = StartCoroutine(TypewriterEffect(message, othersText));

        OnDialogStarted?.Invoke();
    }

    /// <summary>
    /// 隐藏所有对话框（无淡出，瞬间隐藏）
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
            selfDialogCanvasGroup.alpha = 0f;
            dialogBoxSelf.SetActive(false);
        }

        if (dialogBoxOthers != null && dialogBoxOthers.activeSelf)
        {
            othersDialogCanvasGroup.alpha = 0f;
            dialogBoxOthers.SetActive(false);
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
    /// 快速显示玩家对话框（无打字机、无淡入淡出）
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

        // 直接显示面板
        dialogBoxSelf.SetActive(true);
        if (selfDialogCanvasGroup != null)
        {
            selfDialogCanvasGroup.alpha = 1f;
        }

        OnDialogStarted?.Invoke();
    }

    /// <summary>
    /// 快速显示NPC对话框（无打字机、无淡入淡出）
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

        // 直接显示面板
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

    public void SetScore(int score)
    {
        currentScore = score;
        if (currentScore > highScore)
            highScore = currentScore;
        UpdateScoreDisplay();
    }

    public void AddScore(int points)
    {
        currentScore += points;
        if (currentScore > highScore)
            highScore = currentScore;
        UpdateScoreDisplay();
        StartCoroutine(ScorePopupAnimation(points));
    }

    public void SetTargetScore(int target)
    {
        targetScore = target;
        UpdateScoreDisplay();
    }

    public void SetAimText(string aim)
    {
        currentAim = aim;
        if (aimText != null) aimText.text = aim;
    }

    public void ShowScoreAimPanel(bool show)
    {
        if (scoreAimPanel != null)
            scoreAimPanel.SetActive(show);
    }

    public void ResetScore()
    {
        currentScore = 0;
        UpdateScoreDisplay();
    }

    public void ResetHighScore()
    {
        highScore = 0;
        UpdateScoreDisplay();
    }

    public bool IsTargetReached()
    {
        return currentScore >= targetScore;
    }

    #endregion

    #region 辅助方法

    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
            scoreText.text = $"分数: {currentScore}";
        if (highScoreText != null)
            highScoreText.text = $"最高分: {highScore}";
    }

    #endregion

    #region 协程方法

    /// <summary>打字机效果（保留，已移除面板渐变动画）</summary>
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
            yield return new WaitForSeconds(1f / typewriterSpeed);
        }
        isTyping = false;
        OnTypingComplete?.Invoke();
    }

    private IEnumerator ScorePopupAnimation(int points)
    {
        yield return null;
    }

    #endregion

    #region 公共属性

    public int CurrentScore => currentScore;
    public int HighScore => highScore;
    public int TargetScore => targetScore;
    public string CurrentAim => currentAim;
    public bool IsTyping => isTyping;

    #endregion

    #region 编辑器测试菜单

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