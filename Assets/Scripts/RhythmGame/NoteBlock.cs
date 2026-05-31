// NoteBlock.cs
// 碰撞检测方案：手部 Collider 碰到 Block 即触发判定

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace RhythmGame
{
    public enum NoteState { Moving, Caught, Missed }

    public class NoteBlock : MonoBehaviour
    {
        [Header("移动参数")]
        [SerializeField] private float moveSpeed = 4f;

        [Header("Miss 判定：Block 超过判定点多远后算 Miss（米）")]
        [SerializeField] private float missDistance = 0.3f;

        [Header("视觉")]
        [SerializeField] private Renderer noteRenderer;
        [SerializeField] private Color movingColor = new Color(0.3f, 0.8f, 1f);
        [SerializeField] private Color caughtColor = Color.green;
        [SerializeField] private Color missedColor = Color.red;
        [SerializeField] private ParticleSystem catchParticles;

        public UnityEvent<NoteBlock> OnCaught = new UnityEvent<NoteBlock>();
        public UnityEvent<NoteBlock> OnMissed = new UnityEvent<NoteBlock>();

        public bool DisableCollisionCatch = false;
        public UnityEvent<NoteBlock> OnArrived = new UnityEvent<NoteBlock>();

        public NoteState State { get; private set; } = NoteState.Moving;
        public TrackType TrackType { get; private set; }
        public HandSide Hand { get; private set; }

        private Vector3 targetPosition;
        private Vector3 moveDirection;
        private bool passedTarget = false;

        public void Initialize(RhythmTrack track, bool isHold = false)
        {
            TrackType      = track.TrackType;
            Hand           = track.ResponsibleHand;
            targetPosition = track.JudgmentPosition;
            moveDirection  = (targetPosition - transform.position).normalized;

            if (noteRenderer != null)
                noteRenderer.material.color = isHold
                    ? new Color(0.6f, 0.9f, 1f)  // 长音：浅蓝
                    : movingColor;                // 普通：正常蓝
        }

        private void Update()
        {
            if (State != NoteState.Moving) return;

            transform.position += moveDirection * moveSpeed * Time.deltaTime;

            // 检查是否越过判定点
            float distToTarget = Vector3.Distance(transform.position, targetPosition);
            Vector3 toTarget = targetPosition - transform.position;

            // 当 Block 越过判定点（方向反转）时开始计距
            if (!passedTarget && Vector3.Distance(transform.position, targetPosition) < 0.05f)
            {
                passedTarget = true;
                OnArrived.Invoke(this);   // ← 新增
            }

            // 越过判定点后超出 missDistance → Miss
            if (passedTarget && distToTarget >= missDistance)
            {
                Miss();
            }
        }

        // ── 碰撞检测 ──────────────────────────────────────
        // NoteBlock 的 Collider 需勾选 IsTrigger
        // 手部对象上需挂载 HandIdentifier 组件
        private void OnTriggerEnter(Collider other)
        {   
            if (DisableCollisionCatch) return;   // ← 长音头部跳过碰撞检测

            if (State != NoteState.Moving) return;

            if (other.GetComponent<HandIdentifier>() == null) return;

            Catch();
        }

        private void Catch()
        {
            if (State != NoteState.Moving) return;
            State = NoteState.Caught;

            if (noteRenderer != null)
                noteRenderer.material.color = caughtColor;

            catchParticles?.Play();
            OnCaught.Invoke(this);
            StartCoroutine(DestroyAfter(0.3f));
        }

        private void Miss()
        {
            if (State != NoteState.Moving) return;
            State = NoteState.Missed;

            if (noteRenderer != null)
                noteRenderer.material.color = missedColor;

            OnMissed.Invoke(this);
            StartCoroutine(DestroyAfter(0.4f));
        }

        private IEnumerator DestroyAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            Destroy(gameObject);
        }
    }
}