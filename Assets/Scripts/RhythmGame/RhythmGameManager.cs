// RhythmGameManager.cs
// 职责：
//   1. 根据谱面在正确时间生成音符
//   2. 订阅每个音符的成功/失败事件
//   3. 维护分数、连击、完成判定

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RhythmGame
{
    public class RhythmGameManager : MonoBehaviour
    {
        [Header("谱面")]
        [SerializeField] private List<BeatmapData> beatmap = new List<BeatmapData>();

        [Header("四条轨道（Inspector 中按 LeftHigh/RightHigh/LeftLow/RightLow 顺序赋值）")]
        [SerializeField] private RhythmTrack[] tracks = new RhythmTrack[4];

        [Header("音乐播放")]
        [SerializeField] private AudioSource musicSource;

        [Header("音符速度（米/秒，与 NoteBlock 保持一致）")]
        [SerializeField] private float noteSpeed = 4f;

        // ── 公开事件 ──────────────────────────────
        public UnityEvent<int> OnComboChanged = new UnityEvent<int>();
        public UnityEvent<int> OnScoreChanged = new UnityEvent<int>();
        public UnityEvent OnSongCompleted = new UnityEvent();

        // ── 运行时状态 ────────────────────────────
        public int Combo { get; private set; }
        public int Score { get; private set; }

        /// <summary>最高连击数</summary>
        public int MaxCombo { get; private set; }

        /// <summary>命中率 0~1</summary>
        public float HitRatio => totalNotes > 0
            ? (float)hitCount / totalNotes
            : 0f;

        private int totalNotes;
        private int settledNotes;   // 已判定（成功+失败）的音符数
        private bool isPlaying;
        private int curLevel = 0;

        private int hitCount = 0;   // 成功命中的音符数（普通+长音）

        public UnityEvent OnGameStarted = new UnityEvent();
        // ─────────────────────────────────────────
        // 启动
        // ─────────────────────────────────────────

        /// <summary>外部调用此方法开始游戏（例如玩家与小象互动后）</summary>

        public void StartGame()
        {
            if (isPlaying || beatmap == null || curLevel >= beatmap.Count) return;
            isPlaying = true;
            //totalNotes = beatmap[curLevel].notes.Count;
            totalNotes = 0;
            foreach (var note in beatmap[curLevel].notes)
                totalNotes += note.isHold ? 2 : 1;

            // 重置所有统计
            settledNotes = 0;
            Combo = 0;
            Score = 0;
            hitCount = 0;
            MaxCombo = 0;

            OnGameStarted.Invoke();
            OnComboChanged.Invoke(0);
            OnScoreChanged.Invoke(0);

            StartCoroutine(PlaybackRoutine());
            curLevel++;
        }

        // ─────────────────────────────────────────
        // 谱面播放
        // ─────────────────────────────────────────

        private IEnumerator PlaybackRoutine()
        {
            // 计算每个音符需要提前多久生成
            // 提前量 = 轨道长度 / 速度
            // 轨道长度由各轨道的生成点到判定点距离决定

            float songClock = 0f;

            // 用一个队列，按 hitTime 顺序处理
            var queue = new Queue<NoteData>(beatmap[curLevel].notes);

            // 启动音乐
            if (musicSource != null && beatmap[curLevel].music != null)
            {
                musicSource.clip = beatmap[curLevel].music;
                musicSource.Play();
            }

            while (queue.Count > 0)
            {
                songClock += Time.deltaTime;

                NoteData next = queue.Peek();
                RhythmTrack track = tracks[(int)next.track];

                // 计算该音符的生成时刻 = hitTime - 飞行时间
                float travelTime = GetTravelTime(track);
                float spawnAt = next.hitTime - travelTime;

                if (songClock >= spawnAt)
                {
                    queue.Dequeue();
                    SpawnNote(track, next);
                }

                yield return null;
            }

            // 等待所有音符判定完毕
            yield return new WaitUntil(() => settledNotes >= totalNotes);
            isPlaying = false;
            OnSongCompleted.Invoke();
        }

        // ─────────────────────────────────────────
        // 音符生成
        // ─────────────────────────────────────────

        private void SpawnNote(RhythmTrack track, NoteData data)
        {
            if (data.isHold)
            {
                HoldNote hold = track.SpawnHoldNote(data.holdDuration, noteSpeed);
                if (hold == null) return;
                //hold.OnCompleted.AddListener(OnHoldCompleted);
                //hold.OnFailed.AddListener(OnHoldFailed);
                hold.HeadBlock.OnCaught.AddListener(OnNoteCaught);
                hold.HeadBlock.OnMissed.AddListener(OnNoteMissed);
                hold.TailBlock.OnCaught.AddListener(OnNoteCaught);
                hold.TailBlock.OnMissed.AddListener(OnNoteMissed);
            }
            else
            {
                NoteBlock note = track.SpawnNote();
                if (note == null) return;
                note.OnCaught.AddListener(OnNoteCaught);
                note.OnMissed.AddListener(OnNoteMissed);
            }
        }

        private float GetTravelTime(RhythmTrack track)
        {
            float dist = Vector3.Distance(track.SpawnPosition, track.JudgmentPosition);
            return dist / noteSpeed;
        }

        // ─────────────────────────────────────────
        // 判定回调
        // ─────────────────────────────────────────

        private void OnNoteCaught(NoteBlock note)
        {
            AudioManagerGlobal.Instance.Play("hit");
            hitCount++;          // ← 新增
            Combo++;
            if (Combo > MaxCombo) MaxCombo = Combo;   // ← 新增
            Score += CalculateScore(Combo);
            settledNotes++;
            OnComboChanged.Invoke(Combo);
            OnScoreChanged.Invoke(Score);
        }

        private void OnNoteMissed(NoteBlock note)
        {
            Combo = 0;
            settledNotes++;
            OnComboChanged.Invoke(0);
        }

        private void OnHoldCompleted(HoldNote hold)
        {
            hitCount++;          // ← 新增
            Combo++;
            if (Combo > MaxCombo) MaxCombo = Combo;   // ← 新增
            Score += CalculateScore(Combo) * 2;
            settledNotes++;
            OnComboChanged.Invoke(Combo);
            OnScoreChanged.Invoke(Score);
        }

        private void OnHoldFailed(HoldNote hold)
        {
            Combo = 0;
            settledNotes++;
            OnComboChanged.Invoke(0);
        }

        private int CalculateScore(int currentCombo)
        {
            // 基础分 100，连击加成
            if (currentCombo >= 20) return 300;
            if (currentCombo >= 10) return 200;
            return 100;
        }
    }
}