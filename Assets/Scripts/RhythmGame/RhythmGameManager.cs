// RhythmGameManager.cs
// ְ��
//   1. ������������ȷʱ����������
//   2. ����ÿ�������ĳɹ�/ʧ���¼�
//   3. ά������������������ж�

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RhythmGame
{
    public class RhythmGameManager : MonoBehaviour
    {
        [Header("����")]
        [SerializeField] private List<BeatmapData> beatmap = new List<BeatmapData>();

        [Header("���������Inspector �а� LeftHigh/RightHigh/LeftLow/RightLow ˳��ֵ��")]
        [SerializeField] private RhythmTrack[] tracks = new RhythmTrack[4];

        [Header("���ֲ���")]
        [SerializeField] private AudioSource musicSource;

        [Header("�����ٶȣ���/�룬�� NoteBlock ����һ�£�")]
        [SerializeField] private float noteSpeed = 4f;

        // ���� �����¼� ������������������������������������������������������������
        public UnityEvent<int> OnComboChanged = new UnityEvent<int>();
        public UnityEvent<int> OnScoreChanged = new UnityEvent<int>();
        public UnityEvent OnSongCompleted = new UnityEvent();

        // ���� ����ʱ״̬ ��������������������������������������������������������
        public int Combo { get; private set; }
        public int Score { get; private set; }

        private int totalNotes;
        private int settledNotes;   // ���ж����ɹ�+ʧ�ܣ���������
        private bool isPlaying;
        private int curLevel = 0;

        // ����������������������������������������������������������������������������������
        // ����
        // ����������������������������������������������������������������������������������

        /// <summary>�ⲿ���ô˷�����ʼ��Ϸ�����������С�󻥶���</summary>
        public void StartGame()
        {
            if (isPlaying || beatmap == null) return;
            isPlaying = true;
            totalNotes = beatmap[curLevel].notes.Count;
            settledNotes = 0;
            Combo = 0;
            Score = 0;

            StartCoroutine(PlaybackRoutine());
            curLevel++;
        }

        // ����������������������������������������������������������������������������������
        // ���沥��
        // ����������������������������������������������������������������������������������

        private IEnumerator PlaybackRoutine()
        {
            // ����ÿ��������Ҫ��ǰ�������
            // ��ǰ�� = ������� / �ٶ�
            // ��������ɸ���������ɵ㵽�ж���������

            float songClock = 0f;

            // ��һ�����У��� hitTime ˳����
            var queue = new Queue<NoteData>(beatmap[curLevel].notes);

            // ��������
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

                // ���������������ʱ�� = hitTime - ����ʱ��
                float travelTime = GetTravelTime(track);
                float spawnAt = next.hitTime - travelTime;

                if (songClock >= spawnAt)
                {
                    queue.Dequeue();
                    SpawnNote(track, next);
                }

                yield return null;
            }

            // �ȴ����������ж����
            yield return new WaitUntil(() => settledNotes >= totalNotes);
            isPlaying = false;
            OnSongCompleted.Invoke();
        }

        // ����������������������������������������������������������������������������������
        // ��������
        // ����������������������������������������������������������������������������������

        private void SpawnNote(RhythmTrack track, NoteData data)
        {
            NoteBlock note = track.SpawnNote();
            if (note == null) return;

            note.OnCaught.AddListener(OnNoteCaught);
            note.OnMissed.AddListener(OnNoteMissed);
        }

        private float GetTravelTime(RhythmTrack track)
        {
            float dist = Vector3.Distance(track.SpawnPosition, track.JudgmentPosition);
            return dist / noteSpeed;
        }

        // ����������������������������������������������������������������������������������
        // �ж��ص�
        // ����������������������������������������������������������������������������������

        private void OnNoteCaught(NoteBlock note)
        {
            Combo++;
            Score += CalculateScore(Combo);
            settledNotes++;

            if(AudioManagerGlobal.Instance != null)
                AudioManagerGlobal.Instance.Play("hit");
            OnComboChanged.Invoke(Combo);
            OnScoreChanged.Invoke(Score);
        }

        private void OnNoteMissed(NoteBlock note)
        {
            Combo = 0;
            settledNotes++;

            OnComboChanged.Invoke(0);
        }

        private int CalculateScore(int currentCombo)
        {
            // ������ 100�������ӳ�
            if (currentCombo >= 20) return 300;
            if (currentCombo >= 10) return 200;
            return 100;
        }
    }
}