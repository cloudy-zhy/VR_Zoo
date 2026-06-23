using System.Collections;
using Core.Event;
using Core.Utils;
using Manager;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace StarlightCollect
{
    /// <summary>
    /// 管理整个游戏流程
    /// 计分UI的管理等等都归这里管控
    /// </summary>
    public class StarlightController : MonoBehaviour
    {
        [SerializeField] private StarlightLevelSO[] levels;   // 拖入 Level1Config, Level2Config
        [SerializeField] private StarlightThrowController throwController;
        [SerializeField] private StarlightUI ui;
        [SerializeField] private Renderer ren;
        [SerializeField] private PlayableDirector director;

        public StarlightLevelSO CurrentLevel => levels[CurrentLevelIndex];
        public int CurrentLevelIndex { get; private set; }

        public int CurrentScore { get; private set; }
        private int m_curTarget;
        private int m_totalScore;

        private void Start()
        {
            GameManager.Event.Register(StarlightConstant.StarLightCollected, OnStarlightCollected);
            
            foreach (StarlightLevelSO level in levels)
                m_totalScore += level.scoreToPass;
            ren.SetFloatDirect("_WaterLevel", 0f);
        }

        private void OnDestroy()
        {
            GameManager.Event.Unregister(StarlightConstant.StarLightCollected, OnStarlightCollected);
        }
        
        [ContextMenu("StartGame")]
        public void StartGame() => StartLevel(0);

        private void StartLevel(int index)
        {
            CurrentLevelIndex = index;
            m_curTarget += CurrentLevel.scoreToPass;
            throwController.ApplyConfig(CurrentLevel);
            throwController.StartThrowGame();
            ui.ShowScore(CurrentScore, m_curTarget);
        }

        private void OnStarlightCollected(EventContext context)
        {
            CurrentScore++;
            ui.ShowDelta(1);
            ui.ShowScore(CurrentScore, m_curTarget);
            ren.SetFloatDirect("_WaterLevel", (float)CurrentScore / m_totalScore);

            if (CurrentScore >= m_curTarget)
                OnLevelPass();
        }

        private void OnLevelPass()
        {
            throwController.StopThrowGame();
            int nextIndex = CurrentLevelIndex + 1;

            if (nextIndex < levels.Length)
            {
                // 进入下一关
                StartLevel(nextIndex);
            }
            else
            {
                // 全部通关
                this.Broadcast(StarlightConstant.GameEnd);
                director.Play();
            }
        }
    }
}