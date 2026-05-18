using Core.Event;
using Manager;
using TMPro;
using UnityEngine;

namespace FruitSlash
{
    /// <summary>
    /// 切水果小游戏的独立计分器，负责基础分、连斩、多斩和终极奖励。
    /// </summary>
    public class FruitSlashScoreController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private TMP_Text deltaLabel;
        [SerializeField] private TMP_Text comboLabel;

        [Header("连斩")]
        [SerializeField] private float comboWindow = 1f;

        [Header("多斩")]
        [SerializeField] private int multiCutBonusPerExtraFruit = 10;

        public int TotalScore { get; private set; }
        public int ComboCount { get; private set; }
        public bool IsLocked { get; private set; }

        private float _lastCutTime = -999f;

        private void Awake()
        {
            RefreshLabels(0, false);
        }

        /// <summary>
        /// 设置世界空间 UI 的文本引用。
        /// </summary>
        public void ConfigureLabels(TMP_Text score, TMP_Text delta, TMP_Text combo)
        {
            scoreLabel = score;
            deltaLabel = delta;
            comboLabel = combo;
            RefreshLabels(0, false);
        }

        /// <summary>
        /// 重置当前小游戏积分。
        /// </summary>
        public void ResetScore()
        {
            TotalScore = 0;
            ComboCount = 0;
            IsLocked = false;
            _lastCutTime = -999f;
            RefreshLabels(0, false);
        }

        /// <summary>
        /// 普通果实或珍稀果实切中后的计分入口。
        /// </summary>
        public int AddFruitScore(FruitSlashFruit fruit, int sameSwingCutCount)
        {
            if (fruit == null || IsLocked)
                return 0;

            int baseScore = fruit.IsRare ? 50 : fruit.BaseScore;
            int comboBonus = CalculateComboBonus();
            int multiBonus = Mathf.Max(0, sameSwingCutCount - 1) * multiCutBonusPerExtraFruit;
            int added = baseScore + comboBonus + multiBonus;

            TotalScore += added;
            RefreshLabels(added, sameSwingCutCount > 1);
            this.Broadcast(FruitSlashEvents.ComboChanged, ComboCount);
            return added;
        }

        /// <summary>
        /// 七彩巨大果串完成后的终极奖励。
        /// </summary>
        public int CompleteRainbowBunch(int reward)
        {
            if (IsLocked)
                return 0;

            IsLocked = true;
            ComboCount = 0;
            TotalScore += reward;
            RefreshLabels(reward, false);
            return reward;
        }

        private int CalculateComboBonus()
        {
            float now = Time.time;
            ComboCount = now - _lastCutTime <= comboWindow ? ComboCount + 1 : 1;
            _lastCutTime = now;

            if (ComboCount >= 9)
                return 18;
            if (ComboCount >= 5)
                return 12;
            return 8;
        }

        private void RefreshLabels(int delta, bool isMultiCut)
        {
            if (scoreLabel != null)
                scoreLabel.text = TotalScore.ToString();

            if (deltaLabel != null)
                deltaLabel.text = delta > 0 ? "+" + delta : string.Empty;

            if (comboLabel == null)
                return;

            if (isMultiCut)
                comboLabel.text = "Juice Splash!";
            else if (ComboCount > 1)
                comboLabel.text = "Combo x" + ComboCount;
            else
                comboLabel.text = string.Empty;
        }
    }
}
