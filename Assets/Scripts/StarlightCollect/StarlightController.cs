using Core.Event;
using Manager;
using UnityEngine;

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

        private int _currentLevelIndex;
        private int _currentScore;

        public StarlightLevelSO CurrentLevel => levels[_currentLevelIndex];
        public int CurrentLevelIndex => _currentLevelIndex;
        public int CurrentScore => _currentScore;

        private void Start()
        {
            GameManager.Event.Register(StarlightConstant.StarLightCollected, OnStarlightCollected);
            GameManager.Event.Register(StarlightConstant.GameStart, StartGame);
        }

        private void OnDestroy()
        {
            GameManager.Event.Unregister(StarlightConstant.StarLightCollected, OnStarlightCollected);
            GameManager.Event.Unregister(StarlightConstant.GameStart, StartGame);
        }
        
        private void StartGame(EventContext context) => StartLevel(0);

        private void StartLevel(int index)
        {
            _currentLevelIndex = index;
            _currentScore = 0;
            throwController.ApplyConfig(CurrentLevel);
            throwController.StartThrowGame();
        }

        private void OnStarlightCollected(EventContext context)
        {
            _currentScore++;

            if (_currentScore >= CurrentLevel.scoreToPass)
                OnLevelPass();
        }

        private void OnLevelPass()
        {
            throwController.StopThrowGame();
            int nextIndex = _currentLevelIndex + 1;

            if (nextIndex < levels.Length)
            {
                // 进入下一关
                StartLevel(nextIndex);
            }
            else
            {
                // 全部通关
                
            }
        }
    }
}