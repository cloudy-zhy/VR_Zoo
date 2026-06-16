using UnityEngine;
using System.Collections;

public class Scene3Manager : MonoBehaviour
{
    public static Scene3Manager Instance;
    private DialogueController dialogueController;
    public bool gameAllComplete = false;
    private bool endTriggered = false;
    private bool introDialogueDone = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameAllComplete = false;
        endTriggered = false;
        introDialogueDone = false;
        // 延后一帧获取单例，避开初始化时序冲突
        StartCoroutine(TryGetDialogueInstance());
    }

    IEnumerator TryGetDialogueInstance()
    {
        yield return null;
        dialogueController = DialogueController.Instance;
        if (dialogueController == null)
        {
            Debug.LogError("【严重】Scene3Manager 获取 DialogueController 实例失败！对话彻底无法运行");
        }
        else
        {
            Debug.Log("成功获取 DialogueController 实例");
        }
    }

    void Start()
    {
        // 延迟一帧再启动剧情，确保实例就绪
        StartCoroutine(DelayStartStory());
    }

    IEnumerator DelayStartStory()
    {
        yield return new WaitForEndOfFrame();
        if (dialogueController == null) yield break;

        dialogueController.RestartDialogue();
        Debug.Log("准备启动StoryFlow剧情协程");
        StartCoroutine(StoryFlow());
    }

    IEnumerator StoryFlow()
    {
        Debug.Log("剧情启动，等待5秒开口");
        yield return new WaitForSeconds(5f);

        Debug.Log("开始播放第0句");
        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());
        Debug.Log("第0句结束，准备第1句");

        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());
        Debug.Log("第1句全部播放完毕，进入等待游戏通关阶段");
        introDialogueDone = true;

        Debug.Log("当前gameAllComplete初始值：" + gameAllComplete);
        yield return new WaitUntil(() => gameAllComplete);
        Debug.Log("检测到游戏通关，准备进入动画占位");

        yield return new WaitForSeconds(0.5f);
        Debug.Log("动画缓冲结束，开始播放后续收尾台词");

        while (dialogueController.GetCurrentDialogueIndex() < dialogueController.GetTotalDialogueCount())
        {
            yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());
        }
        Debug.Log("全部收尾台词播放完成");
    }

    public void OnGameTotalFinish()
    {
        if (endTriggered) return;
        if (!introDialogueDone)
        {
            Debug.Log("拦截：开场0、1台词未播放完成，忽略提前通关信号");
            return;
        }

        endTriggered = true;
        gameAllComplete = true;
        Debug.Log("正式标记游戏通关，等待剧情继续");
    }
}