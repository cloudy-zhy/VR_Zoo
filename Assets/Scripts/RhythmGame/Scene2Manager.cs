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
        // 第一次对话
        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());

        // 第二次对话
        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());

        billboard.SetActive(true);
        progressBar.SetDuration(ChapterDuration[0]);   // 重置进度条

        // 第一次节奏游戏
        gameEnded = false;
        rhythmGameManager.OnSongCompleted.AddListener(OnGameEnded);
        rhythmGameManager.StartGame();
        yield return new WaitUntil(() => gameEnded);
        rhythmGameManager.OnSongCompleted.RemoveListener(OnGameEnded);


        // 第三次对话
        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());

        billboard.SetActive(true);
        progressBar.SetDuration(ChapterDuration[1]);   // 重置进度条

        // 第二次节奏游戏
        gameEnded = false;
        rhythmGameManager.OnSongCompleted.AddListener(OnGameEnded);
        rhythmGameManager.StartGame();
        yield return new WaitUntil(() => gameEnded);
        rhythmGameManager.OnSongCompleted.RemoveListener(OnGameEnded);

        // 第四次对话
        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());

    }

    void OnGameEnded()
    {
        gameEnded = true;
    }
}