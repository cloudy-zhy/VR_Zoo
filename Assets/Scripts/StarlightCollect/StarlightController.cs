using System.Collections;
using Core.Event;
using Manager;
using UnityEngine;
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

        public StarlightLevelSO CurrentLevel => levels[CurrentLevelIndex];
        public int CurrentLevelIndex { get; private set; }

        public int CurrentScore { get; private set; }

        public int CurrentLevelScore { get; private set; }

        private void Start()
        {
            GameManager.Event.Register(StarlightConstant.StarLightCollected, OnStarlightCollected);
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
            CurrentLevelScore = 0;
            throwController.ApplyConfig(CurrentLevel);
            throwController.StartThrowGame();
            ui.ShowScore(CurrentScore, CurrentLevel.scoreToPass);
        }

        private void OnStarlightCollected(EventContext context)
        {
            CurrentScore++;
            CurrentLevelScore++;
            ui.ShowDelta(1);
            ui.ShowScore(CurrentScore, CurrentLevel.scoreToPass);

            if (CurrentLevelScore >= CurrentLevel.scoreToPass)
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
                StartCoroutine(TmpGameEndCoroutine());
            }
        }

        private IEnumerator TmpGameEndCoroutine()
        {
            yield return new WaitForSeconds(2f);
            SceneManager.LoadScene("EndScene");
        }
    }
}