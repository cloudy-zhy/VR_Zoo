using Core.Utils;
using DG.Tweening;
using Slingshot;
using TMPro;
using UnityEngine;

namespace StarlightCollect
{
    public class StarlightUI : AlwaysFacingCam
    {
        // ─── 序列化字段 ──────────────────────────────────────────────────────

        [Header("文字组件")]
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private TMP_Text scoreDeltaLabel;

        [Header("分数动画")]
        [Tooltip("得分时主分数的弹性放大倍率。")]
        [SerializeField] private float scorePunchScale   = 0.4f;
        [Tooltip("弹性放大持续时长（秒）。")]
        [SerializeField] private float scorePunchDuration = 3.5f;

        [Header("颜色主题")]
        [SerializeField] private Color scoreColor  = new(1f, 0.85f, 0.1f);

        private Vector3  _scoreLabelOriginScale = Vector3.one;
        private Tweener  _scorePunchTween;
        private Sequence _deltaSequence;

        private SlingshotScore _slingshotScore;

        // ─── 生命周期 ────────────────────────────────────────────────────────

        public override void Awake()
        {
            base.Awake();
            _scoreLabelOriginScale = Vector3.one;
            
            _slingshotScore = GetComponentInChildren<SlingshotScore>();
            scoreLabel.text = "0";
            scoreLabel.color = scoreColor;
        }

        private void OnDestroy()
        {
            _scorePunchTween?.Kill();
        }
        
        public void ShowScore(int curScore, int tarScore)
        {
            scoreLabel.text  = curScore + "/" + tarScore;

            // 打断上一次动画后重新播放弹性冲击
            _scorePunchTween?.Kill();
            scoreLabel.transform.localScale = _scoreLabelOriginScale;
            _scorePunchTween = scoreLabel.transform
                .DOPunchScale(Vector3.one * scorePunchScale, scorePunchDuration, vibrato: 6, elasticity: 0.5f)
                .SetLink(gameObject);
        }
        public void ShowDelta(int delta)
        {
            _slingshotScore.PlayAddScoreAni(delta);
        }
    }
}