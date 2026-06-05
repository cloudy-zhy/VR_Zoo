using UnityEngine;

namespace RhythmGame
{
    public class RhythmTrack : MonoBehaviour
    {
        [Header("轨道配置")]
        [SerializeField] private TrackType trackType;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform judgmentPoint;
        [SerializeField] private float judgmentRadius = 0.25f;

        [Header("音符预制体")]
        [SerializeField] private GameObject noteBlockPrefab;

        public TrackType TrackType => trackType;
        public HandSide ResponsibleHand => TrackHelper.GetHand(trackType);
        public Vector3 SpawnPosition => spawnPoint.position;
        public Vector3 JudgmentPosition => judgmentPoint.position;
        public float JudgmentRadius => judgmentRadius;

        [Header("长音支持")]
        [SerializeField] private JudgmentZoneTrigger judgmentZoneTrigger;

        public JudgmentZoneTrigger ZoneTrigger => judgmentZoneTrigger;

        private void Start()
        {
            // 删掉：HandPositionTracker.Instance.RegisterTrack(this);
        }

        // 删掉整个 Update()，不再需要检测手部区域高亮

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

        public HoldNote SpawnHoldNote(float holdDuration, float noteSpeed)
        {
            Vector3 spawnDir  = (judgmentPoint.position - spawnPoint.position).normalized;
            float   tailOffset = holdDuration * noteSpeed;

            // 头部
            GameObject headObj = Instantiate(noteBlockPrefab, spawnPoint.position, Quaternion.identity);
            NoteBlock  head    = headObj.GetComponent<NoteBlock>();
            head.Initialize(this, isHold: true);

            // 尾部：在头部生成点后方，晚 holdDuration 秒到达
            Vector3    tailSpawn = spawnPoint.position - spawnDir * tailOffset;
            GameObject tailObj   = Instantiate(noteBlockPrefab, tailSpawn, Quaternion.identity);
            NoteBlock  tail      = tailObj.GetComponent<NoteBlock>();
            tail.Initialize(this, isHold: true);

            // HoldNote 容器
            GameObject holdObj  = new GameObject("HoldNote");
            HoldNote   holdNote = holdObj.AddComponent<HoldNote>();
            holdNote.Initialize(TrackType, head, tail, judgmentZoneTrigger);

            return holdNote;
        }

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