using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace RhythmGame
{
    public enum HoldState { Approaching, Holding, Completed, Failed }

    public class HoldNote : MonoBehaviour
    {
        [Header("连接线参数")]
        [SerializeField] private int lineCount = 2;
        [SerializeField] private float lineSpread = 0.06f;
        [SerializeField] private float lineWidth = 0.02f;
        [SerializeField] private Material lineMaterial;

        [Header("颜色")]
        [SerializeField] private Color holdLineColor = new Color(0.6f, 0.9f, 1f, 0.8f);
        [SerializeField] private Color holdingColor = new Color(0.2f, 1f, 0.5f, 0.9f);
        [SerializeField] private Color failedLineColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        public UnityEvent<HoldNote> OnCompleted = new UnityEvent<HoldNote>();
        public UnityEvent<HoldNote> OnFailed = new UnityEvent<HoldNote>();

        public HoldState State { get; private set; } = HoldState.Approaching;
        public TrackType TrackType { get; private set; }

        public NoteBlock HeadBlock => headBlock;
        public NoteBlock TailBlock => tailBlock;

        private NoteBlock headBlock;
        private NoteBlock tailBlock;
        private JudgmentZoneTrigger zoneTrigger;
        private LineRenderer[] lines;

        // ─────────────────────────────────────────
        // 初始化
        // ─────────────────────────────────────────

        public void Initialize(TrackType trackType,
                               NoteBlock head,
                               NoteBlock tail,
                               JudgmentZoneTrigger zone)
        {
            TrackType = trackType;
            headBlock = head;
            tailBlock = tail;
            zoneTrigger = zone;

            // 头部：禁用碰撞触发，禁用自动销毁，等待到达判定区
            head.DisableCollisionCatch = true;
            head.SuppressAutoDestroy = true;
            head.OnArrived.AddListener(_ => OnHeadArrived());
            head.OnMissed.AddListener(_ => OnHeadMissed());

            // 尾部：正常碰撞触发
            // 尾部被接住 → 完成；尾部飞过未接 → 若仍在持握则失败
            tail.OnCaught.AddListener(_ => CompleteHold());
            tail.OnMissed.AddListener(_ => {
                if (State == HoldState.Holding) FailHold();
            });

            SetupLines();
        }

        // ─────────────────────────────────────────
        // 每帧更新
        // ─────────────────────────────────────────

        private void Update()
        {
            // 线条跟随头尾位置
            if (State == HoldState.Approaching || State == HoldState.Holding)
                UpdateLines();

            // 持握中：检测手是否离开判定区
            if (State == HoldState.Holding)
            {
                if (zoneTrigger == null || !zoneTrigger.IsHandPresent)
                    FailHold();
            }
        }

        // ─────────────────────────────────────────
        // 头部到达判定区
        // ─────────────────────────────────────────

        private void OnHeadArrived()
        {
            if (State != HoldState.Approaching) return;

            if (zoneTrigger != null && zoneTrigger.IsHandPresent)
            {
                // 手已在位，开始持握
                State = HoldState.Holding;
                SetLineColor(holdingColor);
                headBlock.ForceCatch();   // 触发计分事件，头部停在判定区
            }
            else
            {
                // 手不在位，整个长音直接失败
                FailHold();
            }
        }

        private void OnHeadMissed()
        {
            // 头部自然飞过判定区未被接住
            FailHold();
        }

        // ─────────────────────────────────────────
        // 完成（尾部被接住）
        // ─────────────────────────────────────────

        private void CompleteHold()
        {
            if (State != HoldState.Holding) return;
            State = HoldState.Completed;
            OnCompleted.Invoke(this);
            DestroyAll();
        }

        // ─────────────────────────────────────────
        // 失败
        // ─────────────────────────────────────────

        private void FailHold()
        {
            if (State == HoldState.Failed || State == HoldState.Completed) return;
            State = HoldState.Failed;

            // 手动触发未结算的音符的 Miss 事件（让 RhythmGameManager 正确计数）
            if (headBlock != null && headBlock.State == NoteState.Moving)
            {
                headBlock.OnMissed.Invoke(headBlock);
                headBlock.gameObject.SetActive(false);
            }
            if (tailBlock != null && tailBlock.State == NoteState.Moving)
            {
                tailBlock.OnMissed.Invoke(tailBlock);
                tailBlock.gameObject.SetActive(false);
            }

            SetLineColor(failedLineColor);
            OnFailed.Invoke(this);
            DestroyAll();
        }

        // ─────────────────────────────────────────
        // 统一销毁：头部、尾部、连线、HoldNote 自身
        // ─────────────────────────────────────────

        private void DestroyAll()
        {
            HideLines();
            if (headBlock != null) { Destroy(headBlock.gameObject); headBlock = null; }
            if (tailBlock != null) { Destroy(tailBlock.gameObject); tailBlock = null; }
            Destroy(gameObject);
        }

        // ─────────────────────────────────────────
        // 连接线
        // ─────────────────────────────────────────

        private void SetupLines()
        {
            lines = new LineRenderer[lineCount];
            for (int i = 0; i < lineCount; i++)
            {
                GameObject obj = new GameObject($"HoldLine_{i}");
                obj.transform.SetParent(transform);

                LineRenderer lr = obj.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = lineWidth;
                lr.endWidth = lineWidth;
                lr.startColor = holdLineColor;
                lr.endColor = holdLineColor;
                lr.useWorldSpace = true;
                lr.material = lineMaterial != null
                    ? lineMaterial
                    : new Material(Shader.Find("Sprites/Default"));

                lines[i] = lr;
            }
        }

        private void UpdateLines()
        {
            if (headBlock == null || tailBlock == null) return;

            Vector3 trackDir = (tailBlock.transform.position
                              - headBlock.transform.position).normalized;
            Vector3 sideAxis = Vector3.Cross(trackDir, Vector3.up).normalized;
            if (sideAxis == Vector3.zero) sideAxis = Vector3.right;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null) continue;
                float offset = (i - (lineCount - 1) * 0.5f) * lineSpread;
                Vector3 shift = sideAxis * offset;
                lines[i].SetPosition(0, headBlock.transform.position + shift);
                lines[i].SetPosition(1, tailBlock.transform.position + shift);
            }
        }

        private void SetLineColor(Color color)
        {
            if (lines == null) return;
            foreach (var lr in lines)
            {
                if (lr == null) continue;
                lr.startColor = color;
                lr.endColor = color;
            }
        }

        private void HideLines()
        {
            if (lines == null) return;
            foreach (var lr in lines)
                if (lr != null) lr.enabled = false;
        }

        private void OnDestroy()
        {
            if (lines == null) return;
            foreach (var lr in lines)
                if (lr != null) Destroy(lr.gameObject);
        }
    }
}
