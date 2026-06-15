// Scene2Manager.cs
using RhythmGame;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Rendering.BoolParameter;

public class Scene2Manager : MonoBehaviour
{
    private RhythmGameManager rhythmGameManager;
    private DialogueController dialogueController;
    private bool gameEnded = false;
    private BillboardProgressBar progressBar;    // 进度条UI

    public GameObject billboard;                 // 进度条示例
    public List<float> ChapterDuration;          // 每一关的时长

    void Start()
    {
        dialogueController = DialogueController.Instance;
        rhythmGameManager = FindObjectOfType<RhythmGameManager>();
        StartCoroutine(ExecuteSequence());
        progressBar = billboard.GetComponent<BillboardProgressBar>();
    }

    IEnumerator ExecuteSequence()
    {
        Debug.Log("【流程开始】初始对话索引：" + dialogueController.GetCurrentDialogueIndex());

        // 第一次对话
        Debug.Log("准备弹出第0句");
        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());
        Debug.Log("第0句播放完毕，当前索引：" + dialogueController.GetCurrentDialogueIndex());

        // 第二次对话
        Debug.Log("准备弹出第1句");
        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());
        Debug.Log("第1句播放完毕，当前索引：" + dialogueController.GetCurrentDialogueIndex());

        billboard.SetActive(true);
        progressBar.SetDuration(ChapterDuration[0]);

        // 第一次节奏游戏
        gameEnded = false;
        rhythmGameManager.OnSongCompleted.AddListener(OnGameEnded);
        rhythmGameManager.StartGame();
        yield return new WaitUntil(() => gameEnded);
        rhythmGameManager.OnSongCompleted.RemoveListener(OnGameEnded);
        Debug.Log("第一轮音游结束，准备弹出第2句");

        // 第三次对话
        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());
        Debug.Log("第2句播放完毕，当前索引：" + dialogueController.GetCurrentDialogueIndex());

        billboard.SetActive(true);
        progressBar.SetDuration(ChapterDuration[1]);

        // 第二次节奏游戏
        gameEnded = false;
        rhythmGameManager.OnSongCompleted.AddListener(OnGameEnded);
        rhythmGameManager.StartGame();
        yield return new WaitUntil(() => gameEnded);
        rhythmGameManager.OnSongCompleted.RemoveListener(OnGameEnded);
        Debug.Log("=====第二轮音游已经结束，即将执行3、4对话=====，当前索引：" + dialogueController.GetCurrentDialogueIndex());

        // 连续两句 3、4
        Debug.Log("准备弹出第3句");
        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());
        Debug.Log("第3句播放完毕，当前索引：" + dialogueController.GetCurrentDialogueIndex());

        Debug.Log("准备弹出第4句");
        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());
        Debug.Log("第4句播放完毕，当前索引：" + dialogueController.GetCurrentDialogueIndex());
    }

    void OnGameEnded()
    {
        gameEnded = true;
    }
}