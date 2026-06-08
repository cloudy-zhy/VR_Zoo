using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static DialogueController;

public class DialogueController : MonoBehaviour
{
    public static DialogueController Instance;
    public UnityEvent OnDialogueEnded = new UnityEvent();
    [System.Serializable]
    public class DialogueEntry
    {
        public bool isPlayerSpeaking = false;  // true=玩家说话，false=NPC说话
        public string speakerName = "";        // 说话者名字
        [TextArea(3, 5)]
        public string dialogueText = "";       // 对话内容
        public float displayDuration = 3.0f;   // 每句话显示时长（秒）
    }

    [Header("对话配置")]
    [SerializeField] private List<DialogueEntry> dialogueSequence = new List<DialogueEntry>(); // 对话序列

    [Header("头像引用")]
    [SerializeField] private Sprite playerPortrait;    // 玩家头像
    [SerializeField] private Sprite npcPortrait;       // NPC头像

    [Header("场景跳转")]
    [SerializeField] private bool sceneChangeFlag = false;// 对话结束后是否跳转
    [SerializeField] private string targetSceneName = "Scene1"; // 目标场景名称
    [SerializeField] private float delayBeforeSceneChange = 2.0f; // 对话结束后延迟跳转

    [Header("自动对话设置")]
    [SerializeField] private bool autoDisplayFlag = true;
    [SerializeField] private float initialDelay = 1.0f; // 开始前的初始延迟
    [SerializeField] private float delayBetweenDialogues = 0.5f; // 对话之间的延迟
    [SerializeField] private float minimumDisplayTime = 2.0f; // 每句话最少显示时间
    [SerializeField] private float timePerCharacter = 0.05f; // 每个字符显示时间（影响自动计算时长）
    [SerializeField] private bool enableLastDialogue = false; // 最后一个对话是否一直显示

    [Header("音频设置")]
    [SerializeField] private AudioSource audioSource;          // 音频源组件
    [SerializeField] private AudioClip[] dialogueAudioClips;   // 每个对话对应的音频剪辑
    [SerializeField] AudioMixerGroup dialogueAudioMixerGroup;  // 对话音频混音器

    [Header("调试")]
    [SerializeField] private bool showDebugLogs = false; // 是否显示调试日志

    // 私有变量
    [Header("对话id")]
    [SerializeField] private int currentDialogueIndex = 0;
    private bool isDialogueActive = false;
    private Coroutine dialogueCoroutine;

    private void Awake()
    {
        Instance = this;
        // 确保 AudioSource 存在
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.outputAudioMixerGroup = dialogueAudioMixerGroup;
            }
        }
    }

    private void Start()
    {
        // 开始对话
        if (showDebugLogs) Debug.Log("对话控制器启动");
        if (autoDisplayFlag) StartDialogueSequence();
    }

    /// <summary>
    /// 开始对话序列
    /// </summary>
    public void StartDialogueSequence()
    {
        if (dialogueCoroutine != null)
        {
            StopCoroutine(dialogueCoroutine);
        }

        dialogueCoroutine = StartCoroutine(DialogueSequenceCoroutine());
    }

    /// <summary>
    /// 对话序列协程
    /// </summary>
    private IEnumerator DialogueSequenceCoroutine()
    {
        isDialogueActive = true;

        // 初始延迟
        yield return new WaitForSeconds(initialDelay);

        // 隐藏所有对话框
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideAllDialogs();
        }

        // 遍历所有对话条目
        for (currentDialogueIndex = 0; currentDialogueIndex < dialogueSequence.Count; currentDialogueIndex++)
        {
            var dialogueEntry = dialogueSequence[currentDialogueIndex];

            if (dialogueEntry == null)
            {
                Debug.LogWarning($"对话条目 {currentDialogueIndex} 为空，跳过");
                continue;
            }

            // 显示当前对话
            ShowDialogue(dialogueEntry);

            // 计算显示时间
            float displayTime = dialogueEntry.displayDuration;
            if (displayTime <= 0)
            {
                // 自动计算：基于文本长度，但不少于最小显示时间
                displayTime = Mathf.Max(minimumDisplayTime, dialogueEntry.dialogueText.Length * timePerCharacter);
            }

            if (showDebugLogs) Debug.Log($"显示对话 {currentDialogueIndex + 1}/{dialogueSequence.Count}，时长: {displayTime:F1}秒");

            // 等待显示时间
            yield return new WaitForSeconds(displayTime);

            // 如果不是最后一条对话，在对话之间添加延迟
            if (currentDialogueIndex < dialogueSequence.Count - 1)
            {
                yield return new WaitForSeconds(delayBetweenDialogues);
            }
        }

        // 对话结束
        if (showDebugLogs) Debug.Log("所有对话结束");

        // 隐藏所有对话框
        if (UIManager.Instance != null && !enableLastDialogue)
        {
            UIManager.Instance.HideAllDialogs();
        }

        isDialogueActive = false;

        // 延迟后跳转场景
        yield return new WaitForSeconds(delayBeforeSceneChange);

        // 跳转到目标场景
        if (!string.IsNullOrEmpty(targetSceneName) && sceneChangeFlag)
        {
            if (showDebugLogs) Debug.Log($"跳转到场景: {targetSceneName}");
            SceneManager.LoadScene(targetSceneName);
        }
        else
        {
            if (string.IsNullOrEmpty(targetSceneName))
                Debug.LogWarning("目标场景名称为空，无法跳转");
            else
                Debug.Log("对话结束后不跳转");
        }
    }

    public void ShowDialogueWithIndex()
    {
        if (currentDialogueIndex >= dialogueSequence.Count) return;
        var dialogueEntry = dialogueSequence[currentDialogueIndex];

        ShowDialogue(dialogueEntry);
        PlayDialogueAudio(currentDialogueIndex);
        float displayTime = dialogueEntry.displayDuration;
        if (displayTime <= 0)
        {
            // 自动计算：基于文本长度，但不少于最小显示时间
            displayTime = Mathf.Max(minimumDisplayTime, dialogueEntry.dialogueText.Length * timePerCharacter);
        }

        if (showDebugLogs) Debug.Log($"显示对话 {currentDialogueIndex + 1}/{dialogueSequence.Count}，时长: {displayTime:F1}秒");
        StartCoroutine(DialogsHideDelay(displayTime));
        currentDialogueIndex++;

        // 跳转到目标场景
        if (!string.IsNullOrEmpty(targetSceneName) && sceneChangeFlag && currentDialogueIndex==dialogueSequence.Count)
        {
            if (showDebugLogs) Debug.Log($"跳转到场景: {targetSceneName}");
            SceneManager.LoadScene(targetSceneName);
        }
        else
        {
            if (string.IsNullOrEmpty(targetSceneName))
                Debug.LogWarning("目标场景名称为空，无法跳转");
            else if (sceneChangeFlag)
                Debug.Log("对话结束后不跳转");
            else
                Debug.Log("对话未结束");
        }
    }

    public IEnumerator ShowDialogueWithIndexAndWait()
    {
        if (currentDialogueIndex >= dialogueSequence.Count) yield break;

        yield return new WaitForSeconds(1.0f);
        var dialogueEntry = dialogueSequence[currentDialogueIndex];
        ShowDialogue(dialogueEntry); // 这里会激活对话框GameObject
        PlayDialogueAudio(currentDialogueIndex);

        // +++ 关键修复：等待一帧，确保被SetActive(true)的UI组件完成本帧渲染更新 +++
        yield return null;

        float displayTime = dialogueEntry.displayDuration;
        if (displayTime <= 0)
        {
            displayTime = Mathf.Max(minimumDisplayTime, dialogueEntry.dialogueText.Length * timePerCharacter);
        }
        if (showDebugLogs) Debug.Log($"显示对话 {currentDialogueIndex + 1}/{dialogueSequence.Count} 时间: {displayTime:F1}");

        // 现在，在对话框确认完成一帧更新、稳定显示后，再开始“多久后隐藏它”的倒计时
        yield return StartCoroutine(DialogsHideDelay(displayTime));
        currentDialogueIndex++;

        // 跳转到目标场景
        if (!string.IsNullOrEmpty(targetSceneName) && sceneChangeFlag && currentDialogueIndex==dialogueSequence.Count)
        {
            // 延迟后跳转场景
            yield return new WaitForSeconds(delayBeforeSceneChange);
            if (showDebugLogs) Debug.Log($"跳转到场景: {targetSceneName}");
            SceneManager.LoadScene(targetSceneName);
        }
        else
        {
            if (string.IsNullOrEmpty(targetSceneName))
                Debug.LogWarning("目标场景名称为空，无法跳转");
            else if (sceneChangeFlag)
                Debug.Log("对话结束后不跳转");
            else
                Debug.Log("对话未结束");
        }
    }

    /// <summary>
    /// 根据索引播放对话音频
    /// </summary>
    private void PlayDialogueAudio(int index)
    {
        if (audioSource == null || dialogueAudioClips == null || index < 0 || index >= dialogueAudioClips.Length)
            return;

        AudioClip clip = dialogueAudioClips[index];
        if (clip != null)
        {
            audioSource.Stop(); // 停止当前播放的音频
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    private IEnumerator DialogsHideDelay(float displayTime)
    {
        yield return new WaitForSeconds(displayTime);
        UIManager.Instance.HideAllDialogs();
        OnDialogueEnded.Invoke();
    }
    /// <summary>
    /// 显示对话
    /// </summary>
    private void ShowDialogue(DialogueEntry dialogueEntry)
    {
        if (UIManager.Instance == null)
        {
            Debug.LogError("UIManager实例未找到！");
            return;
        }

        if (dialogueEntry.isPlayerSpeaking)
        {
            // 显示玩家对话框
            UIManager.Instance.ShowSelfDialog(
                dialogueEntry.dialogueText,
                string.IsNullOrEmpty(dialogueEntry.speakerName) ? "玩家" : dialogueEntry.speakerName,
                playerPortrait
            );
        }
        else
        {
            // 显示NPC对话框
            UIManager.Instance.ShowOthersDialog(
                dialogueEntry.dialogueText,
                string.IsNullOrEmpty(dialogueEntry.speakerName) ? "诺亚" : dialogueEntry.speakerName,
                npcPortrait
            );
        }
    }

    /// <summary>
    /// 添加对话条目
    /// </summary>
    public void AddDialogue(bool isPlayerSpeaking, string text, string speakerName = "", float displayDuration = 0)
    {
        DialogueEntry newEntry = new DialogueEntry
        {
            isPlayerSpeaking = isPlayerSpeaking,
            dialogueText = text,
            speakerName = speakerName,
            displayDuration = displayDuration
        };

        dialogueSequence.Add(newEntry);
    }

    /// <summary>
    /// 设置玩家头像
    /// </summary>
    public void SetPlayerPortrait(Sprite portrait)
    {
        playerPortrait = portrait;
    }

    /// <summary>
    /// 设置NPC头像
    /// </summary>
    public void SetNPCPortrait(Sprite portrait)
    {
        npcPortrait = portrait;
    }

    /// <summary>
    /// 立即结束对话并跳转场景
    /// </summary>
    public void EndDialogueImmediately()
    {
        if (dialogueCoroutine != null)
        {
            StopCoroutine(dialogueCoroutine);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideAllDialogs();
        }

        isDialogueActive = false;

        // 立即跳转场景
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
        }
    }

    /// <summary>
    /// 重新开始对话
    /// </summary>
    public void RestartDialogue()
    {
        currentDialogueIndex = 0;
        if (dialogueCoroutine != null)
        {
            StopCoroutine(dialogueCoroutine);
        }
        StartDialogueSequence();
    }

    /// <summary>
    /// 获取当前对话状态
    /// </summary>
    public bool IsDialogueActive()
    {
        return isDialogueActive;
    }

    /// <summary>
    /// 获取当前对话索引
    /// </summary>
    public int GetCurrentDialogueIndex()
    {
        return currentDialogueIndex;
    }

    /// <summary>
    /// 获取总对话数量
    /// </summary>
    public int GetTotalDialogueCount()
    {
        return dialogueSequence.Count;
    }

    /// <summary>
    /// 设置目标场景
    /// </summary>
    public void SetTargetScene(string sceneName)
    {
        targetSceneName = sceneName;
    }

    /// <summary>
    /// 在编辑器中快速测试
    /// </summary>
    [ContextMenu("测试对话序列")]
    private void TestDialogueSequence()
    {
        if (Application.isPlaying)
        {
            Debug.Log("游戏运行时无法通过编辑器测试");
            return;
        }

        // 添加测试对话
        dialogueSequence.Clear();

        AddDialogue(false, "你好，陌生的旅行者。我是诺亚，这片土地的守护者。", "诺亚", 3.5f);
        AddDialogue(true, "这是哪里？我为什么会在这里？", "玩家", 2.5f);
        AddDialogue(false, "欢迎来到艾瑟拉世界。你被选中来到这里，因为黑暗正在蔓延，我们需要你的帮助。", "诺亚", 4.0f);
        AddDialogue(true, "我能做什么？我只是个普通人。", "玩家", 2.5f);
        AddDialogue(false, "每个人都有自己的力量。现在，是时候开始你的旅程了。祝你好运，勇士。", "诺亚", 4.0f);

        Debug.Log($"已添加 {dialogueSequence.Count} 条测试对话");
    }

    /// <summary>
    /// 在编辑器中清除对话
    /// </summary>
    [ContextMenu("清除所有对话")]
    private void ClearAllDialogues()
    {
        dialogueSequence.Clear();
        Debug.Log("已清除所有对话");
    }

    public void SwitchToNextScene()
    {
        SceneManager.LoadScene(targetSceneName);
    }
}