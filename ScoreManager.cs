using System;
using System.Collections.Generic;
using Core.Utils;
using UnityEngine;

namespace Core.Score
{
    /// <summary>
    /// 计分管理器。
    /// 维护当前总分，并提供基础果实计分与连击奖励。
    /// </summary>
    public class ScoreManager : Singleton<ScoreManager>
    {
        private readonly Dictionary<int, int> _shotFruitCounts = new();
        private int _nextShotId = 1;

        /// <summary>当前总分。</summary>
        public int CurrentScore { get; private set; }

        /// <summary>
        /// 分数变化时触发。
        /// 参数1：当前总分；参数2：本次变化值。
        /// </summary>
        public event Action<int, int> ScoreChanged;

        public void AddScore(int value)
        {
            if (value == 0) return;

            CurrentScore += value;
            ScoreChanged?.Invoke(CurrentScore, value);
        }

        public void ResetScore()
        {
            CurrentScore = 0;
            _shotFruitCounts.Clear();
            ScoreChanged?.Invoke(CurrentScore, 0);
        }

        public int BeginShot()
        {
            int shotId = _nextShotId++;
            _shotFruitCounts[shotId] = 0;
            return shotId;
        }

        public void EndShot(int shotId)
        {
            if (shotId <= 0) return;
            _shotFruitCounts.Remove(shotId);
        }

        public void AddFruitScore(FruitScoreType fruitType, bool isDirectHit, int shotId = 0)
        {
            int scoreValue = fruitType switch
            {
                FruitScoreType.SmallBerryCluster => isDirectHit ? 10 : 5,
                FruitScoreType.LargeFruit => isDirectHit ? 50 : 25,
                FruitScoreType.GoldenFruit => 200,
                _ => 0
            };

            AddScore(scoreValue);

            if (shotId > 0)
                RegisterComboFruit(shotId);
        }

        private void RegisterComboFruit(int shotId)
        {
            if (!_shotFruitCounts.ContainsKey(shotId))
                _shotFruitCounts[shotId] = 0;

            _shotFruitCounts[shotId]++;
            int hitCount = _shotFruitCounts[shotId];

            if (hitCount == 3)
                AddScore(100);
            else if (hitCount > 3)
                AddScore(20);
        }
    }

    public enum FruitScoreType
    {
        SmallBerryCluster,
        LargeFruit,
        GoldenFruit
    }
}
