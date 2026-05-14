// HandPositionTracker.cs
// 职责：每帧读取左右手坐标，判断每只手当前处于哪个判定区域内
// 与 PICO SDK 解耦：只需外部将手部 Transform 赋值进来即可
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace RhythmGame
{
    public class HandPositionTracker : MonoBehaviour
    {
        public static HandPositionTracker Instance { get; private set; }

        [Header("手部 Transform（由 PICO SDK 的手部对象赋值）")]
        [SerializeField] private Transform leftHandTransform;
        [SerializeField] private Transform rightHandTransform;

        // 每条轨道的判定区域由 RhythmTrack 注册进来
        private RhythmTrack[] tracks = new RhythmTrack[4];

        // 每帧缓存结果，避免重复计算
        private TrackType? _leftHandZone;
        private TrackType? _rightHandZone;

        /// <summary>左手当前所在区域，不在任何区域则为 null</summary>
        public TrackType? LeftHandZone => _leftHandZone;

        /// <summary>右手当前所在区域，不在任何区域则为 null</summary>
        public TrackType? RightHandZone => _rightHandZone;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>由 RhythmTrack 在 Start 时调用，注册自己</summary>
        public void RegisterTrack(RhythmTrack track)
        {
            tracks[(int)track.TrackType] = track;
        }

        private void Update()
        {
            _leftHandZone = DetectZone(leftHandTransform);
            _rightHandZone = DetectZone(rightHandTransform);
        }

        private TrackType? DetectZone(Transform hand)
        {
            if (hand == null) return null;

            float nearest = float.MaxValue;
            TrackType? result = null;

            foreach (var track in tracks)
            {
                if (track == null) continue;

                float dist = Vector3.Distance(hand.position, track.JudgmentPosition);
                if (dist <= track.JudgmentRadius && dist < nearest)
                {
                    nearest = dist;
                    result = track.TrackType;
                }
            }

            return result;
        }

        /// <summary>
        /// 判断指定手是否在指定轨道的判定区内
        /// </summary>
        public bool IsHandInZone(HandSide hand, TrackType track)
        {
            return hand == HandSide.Left
                ? _leftHandZone == track
                : _rightHandZone == track;
        }

        // ── PICO SDK 接入点 ──────────────────────────
        // 如果使用 PICO 的手部骨骼而非 Transform，
        // 在此处替换坐标来源，其余逻辑不变。
        // 示例（取消注释后替换 Update 中的读取方式）：
        //
        // using Unity.XR.PXR;
        // private Vector3 GetHandPosition(HandSide side)
        // {
        //     PXR_HandTracking.GetHandData(
        //         side == HandSide.Left ? HandType.HandLeft : HandType.HandRight,
        //         out HandInfo info);
        //     // 取食指指尖或手掌中心
        //     return info.handJointLocations[(int)HandJoint.JointPalm].pose.position;
        // }
    }
}