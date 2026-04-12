using UnityEngine;

namespace Core.Score
{
    /// <summary>
    /// 可计分目标。
    /// 每个果实只得分一次。
    /// 每发渡渡鸟只有第一次命中果实时算直接命中，之后都算连锁碰撞。
    /// </summary>
    public class ScoreTarget : MonoBehaviour
    {
        [Header("计分设置")]
        [Tooltip("果实类型，不同类型分值不同。")]
        [SerializeField] private FruitScoreType fruitType = FruitScoreType.SmallBerryCluster;

        private bool _hasScored;

        private void OnCollisionEnter(Collision collision)
        {
            TryScore(collision.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryScore(other.gameObject);
        }

        public void ResetTarget()
        {
            _hasScored = false;
        }

        private void TryScore(GameObject hitObject)
        {
            if (_hasScored) return;
            if (ScoreManager.I == null) return;

            bool isDirectHit = false;
            int shotId = 0;

            var shotContext = hitObject.GetComponent<ScoreShotContext>();
            if (shotContext != null)
            {
                shotId = shotContext.ShotId;
                isDirectHit = shotContext.TryConsumeDirectHit();
            }

            _hasScored = true;
            ScoreManager.I.AddFruitScore(fruitType, isDirectHit, shotId);
        }
    }
}
