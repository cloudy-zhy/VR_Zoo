// IceChimeArray.cs
// 冰凌阵管理器 + 节奏调度器
// 职责：
//   1. 在场景中按环形布局生成冰凌
//   2. 解析曲谱（NoteSequence），在正确时间提前触发各冰凌的 PrePlay
//   3. 支持同时亮起最多两根冰凌（双手操作设计）
//   4. 汇总所有冰凌事件，更新连击数，判断全曲完成

using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class IceChimeArray : MonoBehaviour
{
    // ─────────────────────────────────────────
    // Inspector 配置
    // ─────────────────────────────────────────

    [Header("冰凌生成")]
    [SerializeField] private GameObject chimePrefab;       // 预制体需挂载 IceChime + IceChimeStateMachine
    [SerializeField] private Transform centerPoint;       // 环形中心（通常是小猛犸象位置）
    [SerializeField] private float chimeRadius = 1.5f;
    [SerializeField] private int chimeCount = 8;
    [SerializeField] private AudioClip[] scaleClips;       // 按音阶顺序排列的马林巴琴音效

    [Header("节奏参数")]
    [SerializeField] private float startDelay = 1.0f;      // 第一个音符前的等待时间

    // ─────────────────────────────────────────
    // 曲谱数据结构
    // ─────────────────────────────────────────

    [System.Serializable]
    public class NoteData
    {
        [Tooltip("对应的冰凌编号（0 ~ chimeCount-1）")]
        public int chimeIndex;

        [Tooltip("距曲子开始后，该音符应该被演奏的时刻（秒）")]
        public float targetTime;

        [HideInInspector]
        public bool isCompleted;  // 运行时标记：该音符已完成（成功或错过）
    }

    [Header("曲谱（可在 Inspector 手动填写，或由外部赋值）")]
    [SerializeField] private List<NoteData> noteSequence = new List<NoteData>();

    // ─────────────────────────────────────────
    // 运行时状态
    // ─────────────────────────────────────────

    private List<IceChime> chimes = new List<IceChime>();

    private int currentCombo = 0;
    private int maxCombo = 0;
    private int successCount = 0;
    private int missCount = 0;
    private int wrongCount = 0;
    private bool isPlaying = false;

    // ─────────────────────────────────────────
    // 公开事件（供 GameManager、UI 等订阅）
    // ─────────────────────────────────────────

    public UnityEngine.Events.UnityEvent<int> OnComboChanged = new UnityEngine.Events.UnityEvent<int>();
    public UnityEngine.Events.UnityEvent<IceChimeEventArgs> OnComboBreak = new UnityEngine.Events.UnityEvent<IceChimeEventArgs>();
    public UnityEngine.Events.UnityEvent OnSongCompleted = new UnityEngine.Events.UnityEvent();

    // ─────────────────────────────────────────
    // 生命周期
    // ─────────────────────────────────────────

    private void Start()
    {
        BuildCircularArray();
        RegisterAllChimeEvents();
    }

    // ─────────────────────────────────────────
    // 生成冰凌
    // ─────────────────────────────────────────

    private void BuildCircularArray()
    {
        for (int i = 0; i < chimeCount; i++)
        {
            float angle = i * (360f / chimeCount) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * chimeRadius;
            Vector3 pos = centerPoint.position + offset;

            // 面朝中心
            Quaternion rot = Quaternion.LookRotation(centerPoint.position - pos, Vector3.up);

            GameObject obj = Instantiate(chimePrefab, pos, rot, transform);
            obj.name = $"IceChime_{i}";

            IceChime chime = obj.GetComponent<IceChime>();
            chime.chimeIndex = i;
            chime.chimeSound = scaleClips != null && scaleClips.Length > 0
                ? scaleClips[i % scaleClips.Length]
                : null;

            chimes.Add(chime);
        }
    }

    // ─────────────────────────────────────────
    // 事件注册（统一在此处理连击与完成判定）
    // ─────────────────────────────────────────

    private void RegisterAllChimeEvents()
    {
        foreach (var chime in chimes)
        {
            var sm = chime.StateMachine;

            sm.Events.OnSuccess.AddListener(args =>
            {
                successCount++;
                currentCombo++;
                if (currentCombo > maxCombo) maxCombo = currentCombo;
                OnComboChanged.Invoke(currentCombo);
                TryCompleteCheck();
            });

            sm.Events.OnMissed.AddListener(args =>
            {
                missCount++;
                BreakCombo(args);
                TryCompleteCheck();
            });

            sm.Events.OnWrongHit.AddListener(args =>
            {
                wrongCount++;
                // 按照设计：按错冰凌无惩罚，不重置连击，不计入 miss
                // 若需要重置连击可取消下一行注释
                // BreakCombo(args);
            });
        }
    }

    private void BreakCombo(IceChimeEventArgs args)
    {
        currentCombo = 0;
        OnComboChanged.Invoke(0);
        OnComboBreak.Invoke(args);
    }

    // ─────────────────────────────────────────
    // 节奏调度
    // ─────────────────────────────────────────

    /// <summary>外部调用此方法开始演奏。</summary>
    public void StartSong()
    {
        if (isPlaying) return;
        isPlaying = true;

        foreach (var note in noteSequence)
            note.isCompleted = false;

        StartCoroutine(SongScheduler());
    }

    /// <summary>
    /// 逐帧检查曲谱，在"targetTime - prePlayDuration"之前触发 PrePlay，
    /// 使光效恰好在 targetTime 时刻到达演奏线。
    /// </summary>
    private IEnumerator SongScheduler()
    {
        float songClock = -startDelay;

        while (isPlaying)
        {
            songClock += Time.deltaTime;

            foreach (var note in noteSequence)
            {
                if (note.isCompleted) continue;

                // 提前 prePlayDuration 秒触发 PrePlay
                // 用冰凌 0 的 prePlayDuration 作为参考（所有冰凌相同）
                float prePlayDur = chimes[note.chimeIndex]
                    .StateMachine.PrePlayProgress == 0f
                    ? 1.5f   // fallback，应与 Inspector 一致
                    : 1.5f;

                if (songClock >= note.targetTime - prePlayDur)
                {
                    chimes[note.chimeIndex].StartPrePlay();
                    note.isCompleted = true;   // 防止重复触发；完成判定由事件处理
                }
            }

            yield return null;
        }
    }

    // ─────────────────────────────────────────
    // 完成判定
    // ─────────────────────────────────────────

    private void TryCompleteCheck()
    {
        int total = noteSequence.Count;
        if (total == 0) return;

        // 所有音符都已到达 Success 或 Missed 状态即视为完成
        if (successCount + missCount >= total)
        {
            isPlaying = false;
            OnSongCompleted.Invoke();
            Debug.Log($"[IceChimeArray] 演奏完成！成功: {successCount}/{total}  " +
                        $"最大连击: {maxCombo}  错误触碰: {wrongCount}");
        }
    }

    // ─────────────────────────────────────────
    // 只读统计
    // ─────────────────────────────────────────
    public int CurrentCombo => currentCombo;
    public int MaxCombo => maxCombo;
    public int SuccessCount => successCount;
    public int MissCount => missCount;
}