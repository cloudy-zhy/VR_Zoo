using RhythmGame;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBeatmap", menuName = "RhythmGame/Beatmap")]
public class BeatmapData : ScriptableObject
{
    [Tooltip("对应的背景音乐")]
    public AudioClip music;

    [Tooltip("BPM（仅参考，实际节点由 hitTime 决定）")]
    public float bpm = 120f;

    [Tooltip("音符列表，按 hitTime 升序排列")]
    public List<NoteData> notes = new List<NoteData>();
}