using System.Collections.Generic;
using UnityEngine;

namespace Core.Trajectory
{
    /// <summary>
    /// 抛物线轨迹预测器（物理步进模拟）。
    ///
    /// 核心职责：给定初始位置与速度，模拟逐帧物理运动，输出路径点列表。
    /// 与渲染完全分离，可独立单元测试。
    ///
    /// 使用方式：
    ///   1. 每帧（拉弦期间）调用 UpdatePreview(startPos, velocity) 实时更新预览；
    ///   2. 发射瞬间调用 HidePreview() 隐藏；
    ///   3. 通过 SetWind() / SetDifficulty() 动态调整模拟参数。
    /// </summary>
    public class TrajectoryPredictor : MonoBehaviour
    {
        // ─── 序列化字段 ──────────────────────────────────────────────────────

        [Header("物理模拟参数")]
        [Tooltip("模拟步数。步数越多，预测轨迹越长。60步@0.05s ≈ 3秒飞行时间。")]
        [Range(20, 200)]
        [SerializeField] private int   simulationSteps = 90;

        [Tooltip("每步的时间间隔（秒）。越小越精确，但越消耗性能。")]
        [Range(0.01f, 0.1f)]
        [SerializeField] private float timeStep        = 0.05f;

        [Tooltip("碰撞检测的 LayerMask（地面、树干、障碍物等）。")]
        [SerializeField] private LayerMask collisionMask = ~0;

        [Tooltip("球形碰撞检测半径，模拟渡渡鸟的体积（防止轨迹线穿地）。")]
        [Min(0f)]
        [SerializeField] private float colliderRadius  = 0.15f;

        [Header("渲染组件")]
        [SerializeField] private TrajectoryRenderer trajectoryRenderer;

        // ─── 运行时状态 ──────────────────────────────────────────────────────

        /// <summary>当前风力（世界空间加速度，单位 m/s²）。</summary>
        private Vector3 _windAcceleration = Vector3.zero;

        /// <summary>轨迹线是否可见。</summary>
        private bool _isVisible;

        /// <summary>当前难度是否显示轨迹（简单模式开，困难模式关）。</summary>
        private bool _allowedByDifficulty = true;

        // ─── 公开 API ────────────────────────────────────────────────────────

        /// <summary>
        /// 每帧调用：根据当前弹弓状态更新轨迹预览。
        /// 应在拉弓阶段的每帧（由手势系统驱动）调用。
        /// </summary>
        /// <param name="startPos">发射起点（渡渡鸟出膛位置，世界坐标）</param>
        /// <param name="initialVelocity">初始速度向量（方向 × 速度大小，世界坐标）</param>
        public void UpdatePreview(Vector3 startPos, Vector3 initialVelocity)
        {
            if (!_isVisible || !_allowedByDifficulty) return;

            var result = Predict(startPos, initialVelocity);
            trajectoryRenderer.UpdateLine(result);
        }

        /// <summary>
        /// 显示轨迹预览（进入瞄准状态时调用）。
        /// </summary>
        public void ShowPreview()
        {
            _isVisible = true;
            if (_allowedByDifficulty)
                trajectoryRenderer.SetVisible(true);
        }

        /// <summary>
        /// 隐藏轨迹预览（发射瞬间或取消瞄准时调用）。
        /// </summary>
        public void HidePreview()
        {
            _isVisible = false;
            trajectoryRenderer.SetVisible(false);
        }

        /// <summary>
        /// 纯计算接口：预测轨迹并返回结果，不影响渲染状态。
        /// 可用于 AI 计算、难度检测等非渲染用途。
        /// </summary>
        /// <param name="startPos">发射起点</param>
        /// <param name="initialVelocity">初始速度向量</param>
        /// <returns>轨迹预测结果</returns>
        public TrajectoryResult Predict(Vector3 startPos, Vector3 initialVelocity)
        {
            var points  = new List<Vector3>(simulationSteps + 1);
            var pos     = startPos;
            var vel     = initialVelocity;

            Vector3? landingPoint  = null;
            Vector3  landingNormal = Vector3.up;

            points.Add(pos);

            // 总加速度 = 重力 + 风力（每步固定，不考虑空气阻力）
            Vector3 acceleration = Physics.gravity + _windAcceleration;

            for (int i = 0; i < simulationSteps; i++)
            {
                vel += acceleration * timeStep;
                Vector3 nextPos  = pos + vel * timeStep;
                Vector3 moveDir  = (nextPos - pos);
                float   moveDist = moveDir.magnitude;

                // 球形碰撞检测（比 Linecast 更贴近鸟的实际体积）
                if (Physics.SphereCast(
                        pos,
                        colliderRadius,
                        moveDir.normalized,
                        out RaycastHit hit,
                        moveDist,
                        collisionMask))
                {
                    points.Add(hit.point);
                    landingPoint  = hit.point;
                    landingNormal = hit.normal;
                    break;
                }

                pos = nextPos;
                points.Add(pos);
            }

            return new TrajectoryResult(points, landingPoint, landingNormal);
        }

        // ─── 参数设置 API ────────────────────────────────────────────────────

        /// <summary>
        /// 设置风力加速度（高级关卡开启）。
        /// </summary>
        /// <param name="windAcceleration">风力加速度向量（世界空间，单位 m/s²）</param>
        public void SetWind(Vector3 windAcceleration)
        {
            _windAcceleration = windAcceleration;
        }

        /// <summary>清除风力效果（回到无风状态）。</summary>
        public void ClearWind() => SetWind(Vector3.zero);

        /// <summary>
        /// 根据难度设置是否允许显示轨迹预览。
        /// false = 困难模式，彻底隐藏预览线。
        /// </summary>
        public void SetTrajectoryEnabled(bool enable)
        {
            _allowedByDifficulty = enable;
            if (!enable) trajectoryRenderer.SetVisible(false);
        }

        /// <summary>调整模拟精度（性能敏感场景可降低步数）。</summary>
        public void SetSimulationQuality(int steps, float stepTime)
        {
            simulationSteps = Mathf.Clamp(steps,    20,   200);
            timeStep        = Mathf.Clamp(stepTime, 0.01f, 0.1f);
        }
    }
}