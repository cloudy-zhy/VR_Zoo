using UnityEngine;

public class Scene1_OpeningManager : MonoBehaviour
{
    [Header("开场渡渡鸟对话控制器")]
    public DialogueController openingDialogue;
    [Header("原有教程触发脚本（存放你原来点位触发对话的脚本）")]
    public MonoBehaviour originalTriggerScript;

    // 标记：开场过场是否完成
    public static bool OpeningCutsceneFinished = false;

    private void Start()
    {
        // 进入场景先锁定原有触发，不让旧对话能触发
        if (originalTriggerScript != null)
            originalTriggerScript.enabled = false;

        OpeningCutsceneFinished = false;

        // 延迟一小会儿启动开场渡渡鸟对话
        Invoke(nameof(StartOpeningDialogue), 0.3f);
    }

    void StartOpeningDialogue()
    {
        if (openingDialogue != null)
        {
            openingDialogue.StartDialogueSequence();
            // 监听对话结束事件
            openingDialogue.OnDialogueEnded.AddListener(OnOpeningDialogueComplete);
        }
    }

    void OnOpeningDialogueComplete()
    {
        OpeningCutsceneFinished = true;
        // 开场说完了，再启用原来点位触发脚本，后面玩家走到位置才会弹出旧教程对话
        if (originalTriggerScript != null)
            originalTriggerScript.enabled = true;
    }
}