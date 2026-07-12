using Core.Event;
using Core.Utils;
using Org.BouncyCastle.Asn1.Mozilla;
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

    [System.Serializable]
    public class InfiniteModeSettings
    {
        [Header("生成数量范围")]
        public int minCount = 5;
        public int maxCount = 10;

        [Header("位置范围（世界坐标）")]
        public float posXMin = -26.7138f;
        public float posXMax = -22.8988f;
        public float posYMin = 0.1609f;
        public float posYMax = 3.6879f;
        public float posZMin = -4.5700f;
        public float posZMax = 1.3400f;

        [Header("预制体索引范围（0 ~ 2）")]
        public int prefabIndexMin = 0;
        public int prefabIndexMax = 2;
    }
    [Header("无限模式配置")]
    public InfiniteModeSettings infiniteSettings;

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
        // 清空旧果子
        foreach (Transform t in transform)
            Destroy(t.gameObject);
        if (index >= levels.Count)
        {
            LoadInfiniteLevel();
            return;
        }

        LevelData level = levels[index];
        targetScore = level.targetScore;

        // 生成果子
        foreach (var f in level.fruits)
        {
            GameObject prefab = fruitPrefabs[f.prefabIndex];
            Quaternion rot = Quaternion.Euler(f.rotationEuler);
            GameObject go = Instantiate(prefab, f.position, rot, transform);
        }
        if(index>0) GameObject.Find("DodoBird_Chief_UI").GetComponent<SlingshotBirdUI>().InitialScore();
    }

    private void LoadInfiniteLevel()
    {
        targetScore = 0;
        // 使用配置的范围生成随机水果
        int count = Random.Range(infiniteSettings.minCount, infiniteSettings.maxCount + 1);

        for (int i = 0; i < count; i++)
        {
            // 随机位置
            float x = Random.Range(infiniteSettings.posXMin, infiniteSettings.posXMax);
            float y = Random.Range(infiniteSettings.posYMin, infiniteSettings.posYMax);
            float z = Random.Range(infiniteSettings.posZMin, infiniteSettings.posZMax);
            Vector3 pos = new Vector3(x, y, z);

            float rotX = -90f;
            float rotY = 0;
            float rotZ = 0;
            Quaternion rot = Quaternion.Euler(rotX, rotY, rotZ);

            // 随机预制体索引
            int prefabIdx = Random.Range(infiniteSettings.prefabIndexMin, infiniteSettings.prefabIndexMax + 1);
            prefabIdx = Mathf.Clamp(prefabIdx, 0, fruitPrefabs.Length - 1); // 安全保护

            GameObject prefab = fruitPrefabs[prefabIdx];
            Instantiate(prefab, pos, rot, transform);
            targetScore += prefabIdx * 10 + 10;
        }

        Debug.Log("进入无限模式，生成 " + count + " 个随机水果");
        GameObject.Find("DodoBird_Chief_UI").GetComponent<SlingshotBirdUI>().InitialScore();
    }

    public void NextLevel()
    {
        currentLevelIndex++;
        StartCoroutine(LevelSwitchDelay());
    }

    private IEnumerator LevelSwitchDelay()
    {
        yield return new WaitForSeconds(2.0f);
        LoadLevel(currentLevelIndex);
        if(currentLevelIndex<=3) DialogueController.Instance.ShowDialogueWithIndex();
    }

    public void TimeOutPass()
    {
        DialogueController.Instance.ShowLastDialogue();
        trainComeAgain.Play();
        var controller = FindObjectOfType<Slingshot.SlingshotController>();
        if (controller != null && controller.CurrentBird != null)
        {
            controller.Broadcast("DodoBird.OnRelease", controller.CurrentBird);
        }
    }
}