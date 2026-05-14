// NoteBlock.cs
// 职责：
//   1. 从生成点匀速滑向判定区
//   2. 到达判定区时检测手部位置（方案A：手提前放好）
//   3. 发出成功/失败事件并销毁自身

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace RhythmGame
{
    public enum NoteState { Moving, InJudgment, Caught, Missed }

    public class NoteBlock : MonoBehaviour
    {
        [Header("移动参数")]
        [SerializeField] private float moveSpeed = 4f;      // 单位：米/秒

        [Header("判定参数")]
        [SerializeField] private float judgmentWindow = 0.35f;  // 到达后的判定时间窗口（秒）

        [Header("视觉")]
        [SerializeField] private Renderer noteRenderer;
        [SerializeField] private Color movingColor = new Color(0.3f, 0.8f, 1f);
        [SerializeField] private Color caughtColor = Color.green;
        [SerializeField] private Color missedColor = Color.red;
        [SerializeField] private ParticleSystem catchParticles;

        // 事件
        public UnityEvent<NoteBlock> OnCaught = new UnityEvent<NoteBlock>();
        public UnityEvent<NoteBlock> OnMissed = new UnityEvent<NoteBlock>();

        // 运行时状态
        public NoteState State { get; private set; } = NoteState.Moving;
        public TrackType TrackType { get; private set; }
        public HandSide Hand { get; private set; }

        private Vector3 targetPosition;   // 判定区中心
        private float judgmentRadius;
        private RhythmTrack parentTrack;

        // ─────────────────────────────────────────
        // 初始化
        // ─────────────────────────────────────────

        /// <summary>由 RhythmTrack.SpawnNote() 调用</summary>
        public void Initialize(RhythmTrack track)
        {
            parentTrack = track;
            TrackType = track.TrackType;
            Hand = track.ResponsibleHand;
            targetPosition = track.JudgmentPosition;
            judgmentRadius = track.JudgmentRadius;

            if (noteRenderer != null)
                noteRenderer.material.color = movingColor;
        }

        // ─────────────────────────────────────────
        // 移动逻辑
        // ─────────────────────────────────────────

        private void Update()
        {
            if (State != NoteState.Moving) return;

            transform.position = Vector3.MoveTowards(
                transform.position, targetPosition, moveSpeed * Time.deltaTime);

            // 到达判定区
            if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
            {
                transform.position = targetPosition;
                EnterJudgmentZone();
            }
        }

        // ─────────────────────────────────────────
        // 判定逻辑
        // ─────────────────────────────────────────

        private void EnterJudgmentZone()
        {
            State = NoteState.InJudgment;

            // 立即检测一次（手已提前放好的情况）
            if (HandPositionTracker.Instance.IsHandInZone(Hand, TrackType))
            {
                Catch();
                return;
            }

            // 开启时间窗口，持续等待手部进入
            StartCoroutine(JudgmentWindowRoutine());
        }

        private IEnumerator JudgmentWindowRoutine()
        {
            float elapsed = 0f;

            while (elapsed < judgmentWindow)
            {
                if (HandPositionTracker.Instance.IsHandInZone(Hand, TrackType))
                {
                    Catch();
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            Miss();
        }

        // ─────────────────────────────────────────
        // 结果处理
        // ─────────────────────────────────────────

        private void Catch()
        {
            if (State == NoteState.Caught || State == NoteState.Missed) return;
            State = NoteState.Caught;

            if (noteRenderer != null)
                noteRenderer.material.color = caughtColor;

            catchParticles?.Play();
            OnCaught.Invoke(this);

            StartCoroutine(DestroyAfter(0.3f));
        }

        private void Miss()
        {
            if (State == NoteState.Caught || State == NoteState.Missed) return;
            State = NoteState.Missed;

            if (noteRenderer != null)
                noteRenderer.material.color = missedColor;

            OnMissed.Invoke(this);

            StartCoroutine(DestroyAfter(0.5f));
        }

        private IEnumerator DestroyAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            Destroy(gameObject);
        }
    }
}