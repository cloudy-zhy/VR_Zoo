using Core.Utils;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Slingshot
{
    /// <summary>
    /// 酋长鸟头顶的世界空间 UI。
    /// 纯显示层：只负责动画和文字更新，不持有任何游戏逻辑。
    ///
    /// 预制体结构：
    ///   ChiefBirdUI (此组件)
    ///   └── ScoreLabel      : TextMeshPro  ← 分数数字
    ///   └── ScoreDeltaLabel : TextMeshPro  ← 浮动加分（+10 / +50 / +200）
    ///
    /// 由 ChiefBird 驱动，调用 ShowScore() / ShowDelta() 即可。
    /// </summary>
    public class SlingshotBirdUI : AlwaysFacingCam
    {
        // ─── 序列化字段 ──────────────────────────────────────────────────────

        [Header("文字组件")]
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private TMP_Text scoreDeltaLabel;

        // [Header("Billboard（始终朝向 VR 摄像机）")]
        // [Tooltip("留空则运行时自动绑定 Camera.main。\n" +
        //          "PICO 项目建议手动指定 CenterEyeAnchor。")]
        // [SerializeField] private Transform vrCamera;

        [Header("分数动画")]
        [Tooltip("得分时主分数的弹性放大倍率。")]
        [SerializeField] private float scorePunchScale   = 0.4f;
        [Tooltip("弹性放大持续时长（秒）。")]
        [SerializeField] private float scorePunchDuration = 3.5f;

        // [Header("浮动加分动画")]
        // [Tooltip("加分标签向上漂移的距离（世界单位）。")]
        // [SerializeField] private float deltaFloatHeight  = 0.25f;
        // [Tooltip("加分标签的总生命周期（秒）。")]
        // [SerializeField] private float deltaLifetime     = 2.5f;
        // [Tooltip("开始淡出的时间点（占 deltaLifetime 的比例）。")]
        // [Range(0f, 1f)]
        // [SerializeField] private float deltaFadeStart    = 0.5f;

        [Header("颜色主题")]
        [SerializeField] private Color colorNormal  = Color.white;
        [SerializeField] private Color colorGolden  = new Color(1f, 0.85f, 0.1f); // 金果专用
        // [SerializeField] private Color colorCombo   = new Color(0.3f, 1f,   0.5f); // 连击

        // ─── 私有状态 ────────────────────────────────────────────────────────

        private Vector3  _scoreLabelOriginScale = Vector3.one;
        private Tweener  _scorePunchTween;
        private Sequence _deltaSequence;

        private SlingshotScore _slingshotScore;

        // ─── 生命周期 ────────────────────────────────────────────────────────

        public override void Awake()
        {
            base.Awake();
            _scoreLabelOriginScale = Vector3.one;

            // 隐藏浮动标签初始状态
            // SetDeltaAlpha(0f);
            //
            // if (vrCamera == null && Camera.main != null)
            //     vrCamera = Camera.main.transform;
            
            _slingshotScore = GetComponentInChildren<SlingshotScore>();
        }

        // private void LateUpdate()
        // {
        //     // Billboard：使 UI 面板始终朝向玩家摄像机
        //     if (!vrCamera) return;
        //     Vector3 lookDir = transform.position - vrCamera.position;
        //     if (lookDir != Vector3.zero)
        //         transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
        // }

        private void OnDestroy()
        {
            _scorePunchTween?.Kill();
            // _deltaSequence?.Kill();
        }

        // ─── 公开 API（由 ChiefBird 调用）───────────────────────────────────

        /// <summary>
        /// 更新主分数显示，并播放弹性放大动画。
        /// </summary>
        /// <param name="totalScore">当前累计总分</param>
        /// <param name="isGolden">是否为金果得分（触发金色主题）</param>
        public void ShowScore(int totalScore, bool isGolden = false)
        {
            scoreLabel.text  = totalScore.ToString()+"/"+FruitManager.Instance.targetScore.ToString();
            scoreLabel.color = isGolden ? colorGolden : colorNormal;

            // 打断上一次动画后重新播放弹性冲击
            _scorePunchTween?.Kill();
            scoreLabel.transform.localScale = _scoreLabelOriginScale;
            _scorePunchTween = scoreLabel.transform
                .DOPunchScale(Vector3.one * scorePunchScale, scorePunchDuration, vibrato: 6, elasticity: 0.5f)
                .SetLink(gameObject);
        }

        public void InitialScore()
        {
            scoreLabel.text = "0/" + FruitManager.Instance.targetScore.ToString();

        }

        /// <summary>
        /// 播放浮动加分标签（+10 / +50 / +200）。
        /// </summary>
        /// <param name="delta">加分数值</param>
        /// <param name="isGolden">是否为金果</param>
        /// <param name="isCombo">是否为连击奖励</param>
        public void ShowDelta(int delta, bool isGolden = false, bool isCombo = false)
        {
            _slingshotScore.PlayAddScoreAni(delta);
            // // 打断上一次动画
            // _deltaSequence?.Kill();
            // SetDeltaAlpha(1f);
            //
            // scoreDeltaLabel.text  = $"+{delta}";
            // scoreDeltaLabel.color = isGolden ? colorGolden
            //                       : isCombo  ? colorCombo
            //                       : colorNormal;
            //
            // Vector3 startPos = scoreDeltaLabel.transform.localPosition;
            // Vector3 endPos   = startPos + Vector3.up * deltaFloatHeight;
            //
            // float fadeDelay  = deltaLifetime * deltaFadeStart;
            // float fadeDuration = deltaLifetime * (1f - deltaFadeStart);
            //
            // _deltaSequence = DOTween.Sequence()
            //     .Append(scoreDeltaLabel.transform
            //         .DOLocalMove(endPos, deltaLifetime)
            //         .SetEase(Ease.OutCubic))
            //     .Insert(fadeDelay, DOTween.To(
            //         () => scoreDeltaLabel.alpha,
            //         v  => scoreDeltaLabel.alpha = v,
            //         0f, fadeDuration))
            //     .OnComplete(() =>
            //     {
            //         // 动画结束后重置位置，准备下一次播放
            //         scoreDeltaLabel.transform.localPosition = startPos;
            //         SetDeltaAlpha(0f);
            //     })
            //     .SetLink(gameObject);
        }

        // /// <summary>
        // /// 设置摄像机引用（场景切换或 VR Rig 重建时调用）。
        // /// </summary>
        // public void SetCamera(Transform cam) => vrCamera = cam;

        // ─── 私有工具 ────────────────────────────────────────────────────────

        // private void SetDeltaAlpha(float alpha)
        // {
        //     var c = scoreDeltaLabel.color;
        //     c.a = alpha;
        //     scoreDeltaLabel.color = alpha < 0.01f
        //         ? new Color(c.r, c.g, c.b, 0f)
        //         : new Color(c.r, c.g, c.b, alpha);
        // }
    }
}