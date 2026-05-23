using Core.Utils;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;

namespace TimelineSignal
{
    public class Scene0Signal : MonoBehaviour
    {
        #region SerializedFields Variables

        [SerializeField] private PlayerEnterAreaDetector endDetector;
        [SerializeField] private PlayableDirector director;

        #endregion

        void Awake()
        {
            // director = GetComponent<PlayableDirector>();
            endDetector.OnPlayerEnterArea += OnEnterEnd;
        }

        public void StartDialogueSequence()
        {
            DialogueController.Instance.StartDialogueSequence();
        }

        public void StopTimeline()
        {
            director.Stop();
        }

        private async void OnEnterEnd()
        {
            string targetSceneName = "Scene1";
            endDetector.gameObject.SetActive(false);
            // 小等一会，也可以不等，后面写跳转逻辑
            await UniTask.WaitForSeconds(2.5f);
            SceneManager.LoadScene(targetSceneName);
        }
    }
}
