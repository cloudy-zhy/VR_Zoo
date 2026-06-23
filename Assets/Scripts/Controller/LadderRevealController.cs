using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace Controller
{
    /// <summary>
    /// 控制梯子从下往上裁剪显现的演出脚本。
    /// 动画期间启用 ladderView 播放材质裁剪动画，播放完毕后隐藏 ladderView 并启用可交互的 ladderInteractive。
    /// </summary>
    public class LadderRevealController : MonoBehaviour
    {
        [Header("节点配置")]
        [Tooltip("用于播放裁剪动画的虚设梯子节点")]
        [SerializeField] private GameObject ladderView;
        
        [Tooltip("动画播放完毕后显示的真实可交互梯子节点")]
        [SerializeField] private GameObject ladderInteractive;

        [Header("动画配置")]
        [Tooltip("动画时长")]
        [SerializeField] private float duration = 2.0f;
        
        [Tooltip("缓动类型")]
        [SerializeField] private Ease easeType = Ease.OutCubic;

        [Header("发光边界参数设置")]
        [Tooltip("发光边界的厚度")]
        [SerializeField] private float edgeWidth = 0.2f;

        [Header("调试信息")]
        [SerializeField] private float minY = 0f;
        [SerializeField] private float maxY = 5f;

        [Header("事件回调")]
        public UnityEvent onRevealComplete;

        private MaterialPropertyBlock propBlock;
        private static readonly int RevealWorldYID = Shader.PropertyToID("_RevealWorldY");
        private static readonly int EdgeWidthID = Shader.PropertyToID("_EdgeWidth");
        private Renderer[] viewRenderers;
        private float currentRevealY;
        private bool isRevealing = false;

        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
            
            ladderView.SetActive(true);
            viewRenderers = ladderView.GetComponentsInChildren<Renderer>(true);

            // 预先计算高度边界（此时节点是 Active 的，算出的值 100% 准确）
            // CalculateBounds();

            // 计算完毕后，立刻将两个节点都隐藏，确保游戏开始时玩家绝对看不到任何梯子
            ladderInteractive.SetActive(false);
            ladderView.SetActive(false);

            // 初始设置为完全裁剪隐藏状态
            SetRevealHeight(minY - edgeWidth - 0.1f);
        }

        /// <summary>
        /// 自动计算并更新所有 viewRenderer 子物体的合并世界包围盒 Y 轴边界
        /// </summary>
        [ContextMenu("Calculate Bounds")]
        public void CalculateBounds()
        {
            if (ladderView == null) return;
            
            // 重新获取渲染器以防动态改动
            viewRenderers = ladderView.GetComponentsInChildren<Renderer>(true);

            if (viewRenderers is { Length: > 0 })
            {
                float min = float.MaxValue;
                float max = float.MinValue;
                bool hasValidRenderer = false;

                foreach (var r in viewRenderers)
                {
                    min = Mathf.Min(min, r.bounds.min.y);
                    max = Mathf.Max(max, r.bounds.max.y);
                    hasValidRenderer = true;
                }

                if (hasValidRenderer)
                {
                    minY = min;
                    maxY = max;
                }
            }
        }

        /// <summary>
        /// 应用高度和发光宽度参数到材质
        /// </summary>
        private void SetRevealHeight(float height)
        {
            currentRevealY = height;
            if (viewRenderers == null) return;

            foreach (var r in viewRenderers)
            {
                r.GetPropertyBlock(propBlock);
                propBlock.SetFloat(RevealWorldYID, currentRevealY);
                propBlock.SetFloat(EdgeWidthID, edgeWidth);
                r.SetPropertyBlock(propBlock);
            }
        }

        /// <summary>
        /// 播放从下往上显现的动画
        /// </summary>
        [ContextMenu("Play Reveal")]
        public void PlayReveal()
        {
            if (isRevealing) return;
            isRevealing = true;

            // 准备状态
            ladderInteractive.SetActive(false);
            ladderView.SetActive(true);

            // 重新计算一次边界，确保如果在运行中梯子移动了位置依然准确
            // CalculateBounds();

            // 从比最底部更低一点的高度（保证完全看不见）开始
            float startHeight = minY - edgeWidth - 0.05f;
            // 目标高度：到达最顶部，并且把发光边界也移出网格范围
            float targetHeight = maxY + edgeWidth + 0.05f;

            SetRevealHeight(startHeight);

            // 启动插值动画
            DOTween.To(() => currentRevealY, SetRevealHeight, targetHeight, duration)
                .SetEase(easeType)
                .SetLink(gameObject)
                .OnComplete(OnRevealFinished);
        }

        private void OnRevealFinished()
        {
            isRevealing = false;

            // 隐藏 View 节点
            ladderView.SetActive(false);
            // 显示并激活可交互的真实节点
            ladderInteractive.SetActive(true);

            // 触发可能挂载的事件
            onRevealComplete?.Invoke();
            
            // Debug.Log($"[LadderRevealController] 梯子显示动画播放完毕，已激活可交互节点: {ladderInteractive?.name}");
        }
    }
}
