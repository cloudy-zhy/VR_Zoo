// RhythmTrack.cs
// 职责：
//   1. 代表四条轨道之一，持有生成点和判定区位置
//   2. 向 HandPositionTracker 注册自己的判定区
//   3. 提供生成 NoteBlock 的接口

using UnityEngine;

namespace RhythmGame
{
    public class RhythmTrack : MonoBehaviour
    {
        [Header("轨道配置")]
        [SerializeField] private TrackType trackType;
        [SerializeField] private Transform spawnPoint;      // 音符生成位置（远处）
        [SerializeField] private Transform judgmentPoint;   // 判定区位置（正方形线框角点）
        [SerializeField] private float judgmentRadius = 0.25f; // 手部判定半径（米）

        [Header("音符预制体")]
        [SerializeField] private GameObject noteBlockPrefab;

        [Header("判定区视觉（可选）")]
        [SerializeField] private Renderer zoneRenderer;     // 判定区的高亮显示
        [SerializeField] private Color idleColor = new Color(0.2f, 0.6f, 1f, 0.3f);
        [SerializeField] private Color activeColor = new Color(0.2f, 1f, 0.5f, 0.8f);

        public TrackType TrackType => trackType;
        public HandSide ResponsibleHand => TrackHelper.GetHand(trackType);
        public Vector3 SpawnPosition => spawnPoint.position;
        public Vector3 JudgmentPosition => judgmentPoint.position;
        public float JudgmentRadius => judgmentRadius;

        private void Start()
        {
            HandPositionTracker.Instance.RegisterTrack(this);
            SetZoneVisual(false);
        }

        private void Update()
        {
            // 判定区高亮：对应的手在区域内时发光
            bool handInZone = HandPositionTracker.Instance.IsHandInZone(ResponsibleHand, trackType);
            SetZoneVisual(handInZone);
        }

        /// <summary>由 RhythmGameManager 调用，在生成点生成一个音符</summary>
        public NoteBlock SpawnNote()
        {
            if (noteBlockPrefab == null)
            {
                Debug.LogWarning($"[RhythmTrack] {trackType} 没有设置 NoteBlock Prefab");
                return null;
            }

            GameObject obj = Instantiate(noteBlockPrefab, spawnPoint.position, Quaternion.identity);
            NoteBlock note = obj.GetComponent<NoteBlock>();
            note.Initialize(this);
            return note;
        }

        private void SetZoneVisual(bool active)
        {
            if (zoneRenderer == null) return;
            zoneRenderer.material.color = active ? activeColor : idleColor;
        }

        // 编辑器辅助：在 Scene 视图中显示判定区和轨道线
        private void OnDrawGizmos()
        {
            if (judgmentPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(judgmentPoint.position, judgmentRadius);
            }
            if (spawnPoint != null && judgmentPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(spawnPoint.position, judgmentPoint.position);
            }
        }
    }
}