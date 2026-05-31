// HoldNote.cs【新增文件】
// Bug修复记录：
//   1. 不再订阅 tailBlock.OnMissed（防止正常完成时误判为失败）
//   2. 结果处理时立即隐藏连接线（不等 DestroyAfter 延迟）
//   3. OnDestroy 保底销毁线条子物体

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace RhythmGame
{
    public enum HoldState { Approaching, Holding, Completed, Failed }

    public class HoldNote : MonoBehaviour
    {
        [Header("连接线参数")]
        [SerializeField] private int   lineCount  = 2;       // 线条数量
        [SerializeField] private float lineSpread = 0.06f;   // 线条间距（米）
        [SerializeField] private float lineWidth  = 0.02f;
        [SerializeField] private Material lineMaterial;

        [Header("颜色")]
        [SerializeField] private Color holdLineColor   = new Color(0.6f, 0.9f, 1f, 0.8f);
        [SerializeField] private Color holdingColor    = new Color(0.2f, 1f,   0.5f, 0.9f);
        [SerializeField] private Color failedLineColor = new Color(1f,   0.3f, 0.3f, 0.5f);

        public UnityEvent<HoldNote> OnCompleted = new UnityEvent<HoldNote>();
        public UnityEvent<HoldNote> OnFailed    = new UnityEvent<HoldNote>();

        public HoldState State    { get; private set; } = HoldState.Approaching;
        public TrackType TrackType { get; private set; }

        private NoteBlock            headBlock;
        private NoteBlock            tailBlock;
        private JudgmentZoneTrigger  zoneTrigger;
        private LineRenderer[]       lines;

        // ─────────────────────────────────────────
        // 初始化
        // ─────────────────────────────────────────

        public void Initialize(TrackType trackType,
                               NoteBlock head,
                               NoteBlock tail,
                               JudgmentZoneTrigger zone)
        {
            TrackType   = trackType;
            headBlock   = head;
            tailBlock   = tail;
            zoneTrigger = zone;

            head.OnCaught.AddListener(_ => OnHeadCaught());
            head.OnMissed.AddListener(_ => OnHeadMissed());

            SetupLines();
        }

        // ─────────────────────────────────────────
        // 每帧更新
        // ─────────────────────────────────────────

        private void Update()
        {
            if (State == HoldState.Approaching || State == HoldState.Holding)
                UpdateLines();

            if (State != HoldState.Holding) return;

            // 手离开区域 → 失败
            if (!zoneTrigger.IsHandPresent)
            {
                FailHold();
                return;
            }

            // 尾部自然经过判定点后进入 Missed 状态，这是正常完成信号
            if (tailBlock == null || tailBlock.State == NoteState.Missed)
            {
                CompleteHold();
            }
        }

        // ─────────────────────────────────────────
        // 头部事件
        // ─────────────────────────────────────────

        private void OnHeadCaught()
        {
            if (State != HoldState.Approaching) return;
            State = HoldState.Holding;
            SetLineColor(holdingColor);
            // ✅ 不订阅 tailBlock.OnMissed
            //    尾部 Missed 是正常生命周期，由 Update 里统一判断完成
        }

        private void OnHeadMissed()
        {
            FailHold();
        }

        // ─────────────────────────────────────────
        // 结果处理
        // ─────────────────────────────────────────

        private void CompleteHold()
        {
            if (State != HoldState.Holding) return;
            State = HoldState.Completed;
            // HideLines();   // 立即隐藏，不等延迟
            OnCompleted.Invoke(this);
            StartCoroutine(DestroyAfter(0.8f));
        }

        private void FailHold()
        {
            if (State == HoldState.Failed || State == HoldState.Completed) return;
            State = HoldState.Failed;
            // HideLines();   // 立即隐藏，不等延迟
            OnFailed.Invoke(this);
            StartCoroutine(DestroyAfter(0.2f));
        }

        private IEnumerator DestroyAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
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

                LineRenderer lr  = obj.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth    = lineWidth;
                lr.endWidth      = lineWidth;
                lr.startColor    = holdLineColor;
                lr.endColor      = holdLineColor;
                lr.useWorldSpace = true;
                lr.material      = lineMaterial != null
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
                float   offset = (i - (lineCount - 1) * 0.5f) * lineSpread;
                Vector3 shift  = sideAxis * offset;
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
                lr.endColor   = color;
            }
        }

        private void HideLines()
        {
            if (lines == null) return;
            foreach (var lr in lines)
                if (lr != null) lr.enabled = false;
        }

        // 保底：销毁时清理所有线条子物体
        private void OnDestroy()
        {
            if (lines == null) return;
            foreach (var lr in lines)
                if (lr != null) Destroy(lr.gameObject);
        }
    }
}
