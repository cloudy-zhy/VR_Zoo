// TrackDefinitions.cs
// 轨道类型定义和谱面数据结构

using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    // ─────────────────────────────────────────
    // 四条轨道
    // ─────────────────────────────────────────
    public enum TrackType
    {
        LeftHigh = 0,
        RightHigh = 1,
        LeftLow = 2,
        RightLow = 3
    }

    // ─────────────────────────────────────────
    // 每条轨道属于哪只手
    // ─────────────────────────────────────────
    public enum HandSide { Left, Right }

    public static class TrackHelper
    {
        public static HandSide GetHand(TrackType track)
        {
            return (track == TrackType.LeftHigh || track == TrackType.LeftLow)
                ? HandSide.Left
                : HandSide.Right;
        }
    }

    // ─────────────────────────────────────────
    // 单个音符数据
    // ─────────────────────────────────────────
    [System.Serializable]
    public class NoteData
    {
        [Tooltip("所属轨道")]
        public TrackType track;

        [Tooltip("音符应该到达判定线的时刻（距歌曲开始的秒数）")]
        public float hitTime;

        // ↓ 新增
        public bool  isHold       = false;
        public float holdDuration = 0f;   // 仅 isHold=true 时有效（秒）
    }

    // ─────────────────────────────────────────
    // 谱面 ScriptableObject
    // ─────────────────────────────────────────

}