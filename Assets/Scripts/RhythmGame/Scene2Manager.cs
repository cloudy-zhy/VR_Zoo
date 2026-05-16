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

    void Start()
    {
        dialogueController = DialogueController.Instance;
        rhythmGameManager = FindObjectOfType<RhythmGameManager>();
        StartCoroutine(ExecuteSequence());
    }

    IEnumerator ExecuteSequence()
    {
        // 第一次对话
        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());

        // 第二次对话
        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());

        // 第一次节奏游戏
        gameEnded = false;
        rhythmGameManager.OnSongCompleted.AddListener(OnGameEnded);
        rhythmGameManager.StartGame();
        yield return new WaitUntil(() => gameEnded);
        rhythmGameManager.OnSongCompleted.RemoveListener(OnGameEnded);

        // 第三次对话
        yield return StartCoroutine(dialogueController.ShowDialogueWithIndexAndWait());

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