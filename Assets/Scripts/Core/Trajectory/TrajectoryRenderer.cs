using UnityEngine;

namespace Core.Trajectory
{
    /// <summary>
    /// 轨迹预览的渲染组件。
    /// 负责将 TrajectoryResult 中的路径点转换为视觉表现：
    ///   - 虚线/实线轨迹（LineRenderer + 纹理流动动画）
    ///   - 落点标记（可选的贴地圆圈/十字）
    ///   - 根据拉力强度着色（弱黄 → 中黄 → 强紫）
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

        [Tooltip("轨迹线颜色梯度（起点→终点）。")]
        [SerializeField] private Gradient lineColorGradient;

        [Tooltip("轨迹线的宽度曲线（横轴=轨迹进度 0~1，纵轴=线宽）。")]
        [SerializeField] private AnimationCurve lineWidthCurve = new AnimationCurve(
            new Keyframe(0f, 0.045f),
            new Keyframe(1f, 0.018f));

        [Header("流动动画")]
        [Tooltip("启用虚线流动动画（纹理 UV 滚动）。")]
        [SerializeField] private bool enableFlowAnimation = true;

        [Tooltip("纹理流动速度（越大越快）。负值反向流动。")]
        [SerializeField] private float flowSpeed = 1.2f;

        [Header("落点标记")]
        [Tooltip("落点标记 GameObject（预制体实例，拖入 Hierarchy 中已有的对象）。")]
        [SerializeField] private GameObject landingMarker;

        [Header("力度着色（可选）")]
        [Tooltip("启用后，可通过 SetForceRatio() 实时改变轨迹颜色。")]
        [SerializeField] private bool useForceColor = true;
        [SerializeField] private Color weakColor = new Color(1f, 0.94f, 0.65f, 0.95f);
        [SerializeField] private Color midColor = new Color(1f, 0.82f, 0.18f, 0.95f);
        [SerializeField] private Color strongColor = new Color(0.72f, 0.3f, 1f, 0.95f);

        #endregion

        #region PrivateField

        private Material _lineMaterialInstance;
        private float _textureOffset;
        private bool _isVisible;

        #endregion

        #region LifeCycle

        private void Awake()
        {
            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();

            Material sourceMaterial = lineRenderer.sharedMaterial;
            if (sourceMaterial == null || sourceMaterial.shader == null || sourceMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                sourceMaterial = new Material(Shader.Find("Sprites/Default"));
                sourceMaterial.name = "TrajectoryFallbackMaterial";
            }
            else
            {
                sourceMaterial = new Material(sourceMaterial);
            }

            _lineMaterialInstance = sourceMaterial;
            lineRenderer.material = _lineMaterialInstance;
            lineRenderer.widthCurve = lineWidthCurve;
            lineRenderer.colorGradient = lineColorGradient;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.numCapVertices = 2;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            SetVisible(false);
        }

        private void Update()
        {
            if (!_isVisible || !enableFlowAnimation || _lineMaterialInstance == null) return;

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

        public void UpdateLine(TrajectoryResult result)
        {
            if (result == null || result.Points.Count < 2)
            {
                lineRenderer.positionCount = 0;
                SetLandingMarkerVisible(false);
                return;
            }

            int count = result.Points.Count;
            lineRenderer.positionCount = count;

            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
                positions[i] = result.Points[i];
            lineRenderer.SetPositions(positions);

            if (landingMarker != null)
            {
                if (result.HasLanding)
                {
                    SetLandingMarkerVisible(true);
                    if (result.LandingPoint != null)
                        landingMarker.transform.position = result.LandingPoint.Value + result.LandingNormal * 0.02f;
                    landingMarker.transform.rotation = Quaternion.LookRotation(Vector3.forward, result.LandingNormal);
                }
                else
                {
                    SetLandingMarkerVisible(false);
                }
            }
        }

        public void SetForceRatio(float forceRatio)
        {
            if (!useForceColor) return;

            forceRatio = Mathf.Clamp01(forceRatio);
            Color targetColor = forceRatio < 0.5f
                ? Color.Lerp(weakColor, midColor, forceRatio * 2f)
                : Color.Lerp(midColor, strongColor, (forceRatio - 0.5f) * 2f);

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(targetColor, 0f),
                    new GradientColorKey(new Color(targetColor.r, targetColor.g, targetColor.b, 0.55f), 0.72f),
                    new GradientColorKey(new Color(targetColor.r, targetColor.g, targetColor.b, 0f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.45f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            lineRenderer.colorGradient = gradient;
        }

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

            lineColorGradient = new Gradient();
            lineColorGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.35f, 1f), 0f),
                    new GradientColorKey(new Color(0.72f, 0.3f, 1f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
        }
#endif

        #endregion
    }
}
