using Core.Utils;
using Cysharp.Threading.Tasks;
using Entity.DodoBird;
//using UnityEditor.Search;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Slingshot
{
    public class SlingshotSignal : MonoBehaviour
    {
        #region SerializedFields Variables

        [SerializeField] private DodoBird chief;
        [SerializeField] private AlwaysFacingCam facing;
        [SerializeField] private Transform moveToPlayer;
        [SerializeField] private GameObject chiefSayGo;
        [SerializeField] private DodoBird[] otherBirds;
        [SerializeField] private Transform[] slots;
        [SerializeField] private Transform chiefTransform;
        [SerializeField] private PlayerEnterAreaDetector beginDetector;
        [SerializeField] private float goToPosTime;
        [SerializeField] private GameObject controller;
        [SerializeField] private GameObject chiefBird;
        [SerializeField] private PlayerEnterAreaDetector endDetector;

        #endregion
        
        #region Private Variables

        // private PlayableDirector _director;
        
        #endregion

        #region Lifecycle

        void Awake()
        {
            // _director = GetComponent<PlayableDirector>();
            beginDetector.OnPlayerEnterArea += OnEnterBegin;
            endDetector.OnPlayerEnterArea += OnEnterEnd;
        }

        #endregion

        public void OtherBirdsJump()
        {
            foreach (DodoBird bird in otherBirds)
            {
                bird.ani.SetBool("Jump", true);
            }
            chief.ani.SetBool("Idle", false);
        }

        public void ChiefShock()
        {
            chief.ani.SetTrigger("Shock");
            chief.PlayParticle(DodoBirdParticleType.Shock);
        }

        public void ChiefMoveToPlayer()
        {
            // 让酋长去找玩家
            chief.ani.SetBool("Move", true);
            chief.NavAgent.enabled = true;
            chief.NavAgent.SetDestination(moveToPlayer.position);
        }

        public async void ChiefSayAndPoint()
        {
            // 酋长一边说话，一边指指点点
            chief.ani.SetBool("Move", false);
            chief.ani.SetBool("Say", true);
            chief.NavAgent.ResetPath();
            chiefSayGo.SetActive(true);
            // _director.Pause();
            facing.enabled = true;
            
            await UniTask.WaitForSeconds(2.5f);
            beginDetector.gameObject.SetActive(true);
        }

        private async void OnEnterBegin()
        {
            beginDetector.gameObject.SetActive(false);
            // _director.Play();
            chiefSayGo.SetActive(false);
            facing.enabled = false;
            for (int i = 0; i < slots.Length; i++)
            {
                otherBirds[i].ani.SetBool("Jump", false);
                otherBirds[i].ani.SetBool("Move", true);
                otherBirds[i].NavAgent.enabled = true;
                otherBirds[i].NavAgent.SetDestination(slots[i].position);
            }
            chief.ani.SetBool("Say", false);
            chief.ani.SetBool("Move", true);
            chief.NavAgent.SetDestination(chiefTransform.position);
            
            // Debug.Log("Begin");
            await UniTask.WaitForSeconds(goToPosTime);
            // Debug.Log("Over");
            
            DialogueController.Instance.ShowDialogueWithIndex();
            FruitManager.Instance.LoadLevel(FruitManager.Instance.currentLevelIndex);
            
            foreach(var bird in otherBirds)
            {
                bird.ani.SetBool("Move", false);
                bird.gameObject.SetActive(false);
            }
            chief.gameObject.SetActive(false);
            controller.SetActive(true);
            chiefBird.SetActive(true);
        }

        private async void OnEnterEnd()
        {
            string targetSceneName = "Scene2_音游";
            endDetector.gameObject.SetActive(false);
            // 小等一会，也可以不等，后面写跳转逻辑
            await UniTask.WaitForSeconds(2.5f);
            SceneManager.LoadScene(targetSceneName);
        }
    }
}