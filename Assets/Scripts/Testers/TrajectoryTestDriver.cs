using Core.Trajectory;
using UnityEngine;

namespace Testers
{
    public class TrajectoryTestDriver : MonoBehaviour
    {
        [Header("核心系统引用")]
        [Tooltip("拖入挂载了 TrajectoryPredictor 的对象")]
        public TrajectoryPredictor predictor;
        [Tooltip("拖入挂载了 TrajectoryRenderer 的对象")]
        public TrajectoryRenderer rendererObj; 
        [Tooltip("弹弓发射点（轨迹的起点）")]
        public Transform firePoint;
    
        [Header("发射参数配置")]
        [Tooltip("发射方向（局部或世界坐标方向，内部会自动 Normalize 归一化）")]
        public Vector3 launchDirection = new Vector3(0, 1f, 2f);
        
        [Tooltip("当前发射力度大小")]
        [Range(0f, 30f)]
        public float launchForce = 10f;
        
        [Tooltip("最大发射力度（用于计算轨迹颜色渐变的比例）")]
        public float maxForce = 30f;
    
        // 内部状态：当前是否处于“拉弓瞄准”状态
        private bool _isAiming = false;
    
        void Update()
        {
            // -----------------------------------------
            // 1. 按下 Q 键：进入瞄准状态，显示轨迹
            // -----------------------------------------
            if (Input.GetKeyDown(KeyCode.Q))
            {
                _isAiming = true;
                predictor.ShowPreview(); // 调用 Claude 的接口：显示轨迹
                Debug.Log("【测试系统】按下 Q：开始瞄准...");
            }
    
            // -----------------------------------------
            // 2. 瞄准状态中：每帧更新轨迹
            // -----------------------------------------
            if (_isAiming)
            {
                // 确保方向向量有效（避免 (0,0,0) 导致错误）
                Vector3 normalizedDir = launchDirection.normalized;
                if (normalizedDir == Vector3.zero) 
                {
                    normalizedDir = Vector3.forward; // 给个默认前方
                }
    
                // 计算最终的初速度向量：方向 * 力度
                Vector3 initialVelocity = normalizedDir * launchForce;
    
                // 调用 Claude 的接口：实时计算并画线
                predictor.UpdatePreview(firePoint.position, initialVelocity);
    
                // 调用 Claude 的 Renderer 接口：根据当前拉力比例改变颜色（绿->黄->红）
                float pullRatio = launchForce / maxForce;
                rendererObj.SetForceRatio(pullRatio); 
            }
    
            // -----------------------------------------
            // 3. 按下 E 键：释放发射，隐藏轨迹
            // -----------------------------------------
            if (Input.GetKeyDown(KeyCode.E))
            {
                _isAiming = false;
                predictor.HidePreview(); // 调用 Claude 的接口：隐藏轨迹
                
                // 计算最终的速度用于发射
                Vector3 finalVelocity = launchDirection.normalized * launchForce;
                Debug.Log($"【测试系统】按下 E：发射小鸟！初速度为: {finalVelocity}");
                
                // TODO: 未来在这里写代码 -> 实例化小鸟 Prefab -> 获取它的 Rigidbody -> rigidbody.velocity = finalVelocity;
            }
        }
    }
}