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

        public UnityEvent<NoteBlock> OnCaught  = new UnityEvent<NoteBlock>();
        public UnityEvent<NoteBlock> OnMissed  = new UnityEvent<NoteBlock>();
        public UnityEvent<NoteBlock> OnArrived = new UnityEvent<NoteBlock>();

        /// <summary>true 时碰撞不触发 Catch（长音头部用）</summary>
        public bool DisableCollisionCatch = false;

        /// <summary>true 时 Catch 后不自动销毁（长音头部用，由 HoldNote 统一销毁）</summary>
        public bool SuppressAutoDestroy = false;

        public NoteState State     { get; private set; } = NoteState.Moving;
        public TrackType TrackType { get; private set; }
        public HandSide  Hand      { get; private set; }

        private Vector3 targetPosition;
        private Vector3 moveDirection;
        private bool    passedTarget = false;

        public void Initialize(RhythmTrack track, bool isHold = false)
        {
            TrackType      = track.TrackType;
            Hand           = track.ResponsibleHand;
            targetPosition = track.JudgmentPosition;
            moveDirection  = (targetPosition - transform.position).normalized;

            if (noteRenderer != null)
                noteRenderer.material.color = isHold
                    ? new Color(0.6f, 0.9f, 1f)
                    : movingColor;
        }

        private void Update()
        {
            if (State != NoteState.Moving) return;

            transform.position += moveDirection * moveSpeed * Time.deltaTime;

            float distToTarget = Vector3.Distance(transform.position, targetPosition);

            if (!passedTarget && distToTarget < 0.05f)
            {
                passedTarget = true;
                OnArrived.Invoke(this);
            }

            if (passedTarget && distToTarget >= missDistance)
                Miss();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (DisableCollisionCatch) return;
            if (State != NoteState.Moving) return;
            if (other.GetComponent<HandIdentifier>() == null) return;
            Catch();
        }

        /// <summary>供 HoldNote 主动触发头部计分</summary>
        public void ForceCatch() => Catch();

        private void Catch()
        {
            if (State != NoteState.Moving) return;
            State = NoteState.Caught;

            if (noteRenderer != null)
                noteRenderer.material.color = caughtColor;

            catchParticles?.Play();
            OnCaught.Invoke(this);

            // SuppressAutoDestroy=true 时由外部（HoldNote）负责销毁
            if (!SuppressAutoDestroy)
                StartCoroutine(DestroyAfter(0.3f)); // 粒子效果有1s
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