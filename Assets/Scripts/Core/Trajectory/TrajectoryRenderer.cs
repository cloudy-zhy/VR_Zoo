using UnityEngine;

namespace Core.Trajectory
{
    /// <summary>
    /// 轨迹预览的渲染组件。
    /// 负责将 TrajectoryResult 中的路径点转换为视觉表现：
    ///   - 虚线/实线轨迹（LineRenderer + 纹理流动动画）
    ///   - 落点标记（可选的贴地圆圈/十字）
    ///   - 根据拉力强度着色（绿→黄→红渐变）
    ///
    /// 此组件只负责"画"，不做任何物理计算，由 TrajectoryPredictor 驱动。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryRenderer : MonoBehaviour
    {
        #region SerializeField

        [Header("LineRenderer 配置")]
        [Tooltip("LineRenderer 组件（自动获取，无需手动赋值）。")]
        [SerializeField] private LineRenderer lineRenderer;

        [Tooltip("轨迹线颜色梯度（起点→终点）。" +
                 "建议：尾部透明度低于头部，营造消散感。")]
        [SerializeField] private Gradient lineColorGradient;

        [Tooltip("轨迹线的宽度曲线（横轴=轨迹进度 0~1，纵轴=线宽）。")]
        [SerializeField] private AnimationCurve lineWidthCurve = AnimationCurve.Linear(0, 0.4f, 1, 1f);

        [Header("流动动画")]
        [Tooltip("启用虚线流动动画（纹理 UV 滚动）。" +
                 "需要 LineRenderer 的材质使用支持 _MainTex_ST 的 Shader。")]
        [SerializeField] private bool  enableFlowAnimation = true;

        [Tooltip("纹理流动速度（越大越快）。负值反向流动。")]
        [SerializeField] private float flowSpeed           = 1.2f;

        [Header("落点标记")]
        [Tooltip("落点标记 GameObject（预制体实例，拖入 Hierarchy 中已有的对象）。")]
        [SerializeField] private GameObject landingMarker;

        // [Tooltip("落点标记的缩放大小。")]
        // [SerializeField] private float landingMarkerScale = 0.4f;

        [Header("力度着色（可选）")]
        [Tooltip("启用后，可通过 SetForceRatio() 实时改变轨迹颜色。\n" +
                 "颜色将从 weakColor（力度低）过渡到 strongColor（力度满）。")]
        [SerializeField] private bool  useForceColor = true;
        [SerializeField] private Color weakColor     = new Color(0.2f, 0.9f, 0.2f, 0.9f); // 绿
        [SerializeField] private Color midColor      = new Color(0.9f, 0.8f, 0.1f, 0.9f); // 黄
        [SerializeField] private Color strongColor   = new Color(0.9f, 0.2f, 0.1f, 0.9f); // 红

        #endregion
        
        #region PrivateField
        
        private Material _lineMaterialInstance; // 使用实例化材质，避免污染共享材质
        private float    _textureOffset;
        private bool     _isVisible;
        
        #endregion

        #region LifeCycle
        
        private void Awake()
        {
            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();

            // 实例化材质，使纹理 UV 动画互不干扰
            _lineMaterialInstance = lineRenderer.sharedMaterial != null ? new Material(lineRenderer.sharedMaterial) : new Material(Shader.Find("Sprites/Default"));

            lineRenderer.material = _lineMaterialInstance;
            lineRenderer.widthCurve = lineWidthCurve;
            lineRenderer.colorGradient = lineColorGradient;

            SetVisible(false);
        }

        private void Update()
        {
            if (!_isVisible || !enableFlowAnimation) return;

            // UV 流动：产生虚线/点线向前"流动"的视觉效果
            _textureOffset += flowSpeed * Time.deltaTime;
            _lineMaterialInstance.mainTextureOffset = new Vector2(_textureOffset, 0f);
        }

        private void OnDestroy()
        {
            if (_lineMaterialInstance != null)
                Destroy(_lineMaterialInstance);
        }
        
        #endregion

        #region PublicMethod

        /// <summary>
        /// 根据 TrajectoryResult 更新渲染状态。
        /// 每帧由 TrajectoryPredictor 驱动调用。
        /// </summary>
        public void UpdateLine(TrajectoryResult result)
        {
            if (result == null || result.Points.Count < 2)
            {
                lineRenderer.positionCount = 0;
                SetLandingMarkerVisible(false);
                return;
            }

            // 更新路径点
            int count = result.Points.Count;
            lineRenderer.positionCount = count;

            // IReadOnlyList → array（SetPositions 需要数组）
            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
                positions[i] = result.Points[i];
            lineRenderer.SetPositions(positions);

            // 更新落点标记
            if (landingMarker != null)
            {
                if (result.HasLanding)
                {
                    SetLandingMarkerVisible(true);
                    if (result.LandingPoint != null)
                        landingMarker.transform.position = result.LandingPoint.Value + result.LandingNormal * 0.02f;
                    landingMarker.transform.rotation = Quaternion.LookRotation(
                        Vector3.forward, result.LandingNormal);
                    // landingMarker.transform.localScale = Vector3.one * landingMarkerScale;
                }
                else
                {
                    SetLandingMarkerVisible(false);
                }
            }
        }

        /// <summary>
        /// 根据拉力比例实时更新轨迹颜色（绿→黄→红）。
        /// 应与弹弓拉力数据同步调用。
        /// </summary>
        /// <param name="forceRatio">归一化力度，0（最弱）~ 1（最强）</param>
        public void SetForceRatio(float forceRatio)
        {
            if (!useForceColor) return;

            forceRatio = Mathf.Clamp01(forceRatio);
            Color targetColor = forceRatio < 0.5f
                ? Color.Lerp(weakColor,   midColor,    forceRatio * 2f)
                : Color.Lerp(midColor,    strongColor, (forceRatio - 0.5f) * 2f);

            // 保持渐变的整体色调，只改变基础色
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(targetColor, 0f),
                    new GradientColorKey(new Color(targetColor.r, targetColor.g, targetColor.b, 0f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0f,    1f)
                }
            );
            lineRenderer.colorGradient = gradient;
        }

        /// <summary>
        /// 显示或隐藏轨迹预览（包含落点标记）。
        /// </summary>
        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            lineRenderer.enabled = visible;
            SetLandingMarkerVisible(visible && landingMarker != null);

            if (!visible)
            {
                lineRenderer.positionCount = 0;
                _textureOffset = 0f;
                if (_lineMaterialInstance != null)
                    _lineMaterialInstance.mainTextureOffset = Vector2.zero;
            }
        }

        #endregion
        
        #region PrivateMethod
        
        private void SetLandingMarkerVisible(bool visible)
        {
            if (landingMarker != null)
                landingMarker.SetActive(visible);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            lineRenderer = GetComponent<LineRenderer>();

            // 设置编辑器默认渐变，方便初始配置
            lineColorGradient = new Gradient();
            lineColorGradient.SetKeys(
                new[] {
                    new GradientColorKey(Color.green, 0f),
                    new GradientColorKey(Color.green, 1f)
                },
                new[] {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.0f, 1f)
                }
            );
        }
#endif
        
        #endregion
    }
}