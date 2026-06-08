using UnityEngine;

namespace StarlightCollect
{
    [CreateAssetMenu(fileName = "StarlightLevelSO", menuName = "Data/Starlight/LevelConfigSO", order = 0)]
    public class StarlightLevelSO : ScriptableObject
    {
        [Header("通关条件")]
        public int scoreToPass = 10;        // 收集多少颗星光通关

        [Header("投掷参数")]
        public float throwVelocity = 1f;
        public float spawnInterval = 3f;
        public int minRequestCount = 1;
        public int maxRequestCount = 1;
        public Vector2 requestSpacingRange = new Vector2(0.1f, 0.35f);
    }
}