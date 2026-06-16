using Core.Utils;
using Slingshot;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
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

    [SerializeField] private PlayableDirector trainComeAgain;

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
        if(index>0) GameObject.Find("DodoBird_Chief_UI").GetComponent<SlingshotBirdUI>().InitialScore();
    }

    public void NextLevel()
    {
        currentLevelIndex++;
        StartCoroutine(LevelSwitchDelay());
    }

    private IEnumerator LevelSwitchDelay()
    {
        yield return new WaitForSeconds(3.0f);
        LoadLevel(currentLevelIndex);
        DialogueController.Instance.ShowDialogueWithIndex();
        // 原代码：if (currentLevelIndex > 3)
        // 修改为：没有下一关时才判定全部通关
        if (currentLevelIndex > levels.Count)
        {
            // 车回来接人
            trainComeAgain.Play();
        }
    }
}