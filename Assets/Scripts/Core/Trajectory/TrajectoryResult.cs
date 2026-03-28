using System.Collections.Generic;
using UnityEngine;

namespace Core.Trajectory
{
    /// <summary>
    /// 单次轨迹预测的结果数据。
    /// 由 TrajectoryPredictor.Predict() 返回，传给 TrajectoryRenderer 渲染。
    /// </summary>
    public class TrajectoryResult
    {
        /// <summary>
        /// 轨迹路径点列表（世界坐标，按时间顺序排列）。
        /// 包含起点，若发生碰撞则末尾为碰撞点。
        /// </summary>
        public IReadOnlyList<Vector3> Points { get; }

        /// <summary>
        /// 落点坐标（与碰撞体接触的位置）。
        /// 若预测步数内未发生碰撞，则为 null。
        /// </summary>
        public Vector3? LandingPoint { get; }

        /// <summary>轨迹末端是否有有效落点。</summary>
        public bool HasLanding => LandingPoint.HasValue;

        /// <summary>
        /// 落点法线方向（用于贴地落点标记的朝向）。
        /// 无碰撞时为 Vector3.up。
        /// </summary>
        public Vector3 LandingNormal { get; }

        public TrajectoryResult(
            List<Vector3> points,
            Vector3?      landingPoint,
            Vector3       landingNormal = default)
        {
            Points        = points.AsReadOnly();
            LandingPoint  = landingPoint;
            LandingNormal = landingNormal == default ? Vector3.up : landingNormal;
        }
    }
}