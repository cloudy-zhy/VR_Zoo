using UnityEngine;

namespace Core.Score
{
    /// <summary>
    /// 记录一次发射的编号，并维护该发射是否还有“首次直接命中”资格。
    /// 挂在发射物上，在真正发射时调用 BeginShot()。
    /// </summary>
    public class ScoreShotContext : MonoBehaviour
    {
        [Tooltip("启用时自动开始一次新的发射统计。测试阶段可直接勾选。")]
        [SerializeField] private bool beginShotOnEnable;

        /// <summary>当前发射编号。0 表示未开始统计。</summary>
        public int ShotId { get; private set; }

        private bool _canDirectHit;

        private void OnEnable()
        {
            if (beginShotOnEnable)
                BeginShot();
        }

        public void BeginShot()
        {
            if (ScoreManager.I == null)
            {
                ShotId = 0;
                _canDirectHit = false;
                return;
            }

            ShotId = ScoreManager.I.BeginShot();
            _canDirectHit = true;
        }

        public void EndShot()
        {
            if (ScoreManager.I != null && ShotId > 0)
                ScoreManager.I.EndShot(ShotId);

            ShotId = 0;
            _canDirectHit = false;
        }

        /// <summary>
        /// 尝试消耗本次发射的首次直接命中资格。
        /// 第一次调用返回 true，之后都返回 false。
        /// </summary>
        public bool TryConsumeDirectHit()
        {
            if (!_canDirectHit)
                return false;

            _canDirectHit = false;
            return true;
        }
    }
}
