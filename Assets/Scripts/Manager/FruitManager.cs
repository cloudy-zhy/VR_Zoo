using Slingshot;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FruitManager : MonoBehaviour
{
    public static FruitManager Instance;

    [Header("果子预制体（按顺序）")]
    public GameObject[] fruitPrefabs; // 0 / 1 / 2

    [Header("关卡配置")]
    public List<LevelData> levels;
    public int currentLevelIndex = 0;

    [Header("分数")]
    public int targetScore;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        //LoadLevel(currentLevelIndex);
    }

    public void LoadLevel(int index)
    {
        if (index >= levels.Count)
        {
            Debug.Log("通关");
            return;
        }

        LevelData level = levels[index];
        targetScore = level.targetScore;

        // 清空旧果子
        foreach (Transform t in transform)
            Destroy(t.gameObject);

        // 生成果子
        foreach (var f in level.fruits)
        {
            GameObject prefab = fruitPrefabs[f.prefabIndex];
            Quaternion rot = Quaternion.Euler(f.rotationEuler);
            GameObject go = Instantiate(prefab, f.position, rot, transform);
        }
    }

    //public void AddScore(int value)
    //{
    //    currentScore += value;
    //    if (currentScore >= targetScore)
    //        NextLevel();
    //}

    public void NextLevel()
    {
        currentLevelIndex++;
        LoadLevel(currentLevelIndex);
    }
}