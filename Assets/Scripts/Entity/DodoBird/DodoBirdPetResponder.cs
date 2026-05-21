using Core.Event;
using Manager;
using Pet;
using UnityEngine;

namespace Entity.DodoBird
{
    /// <summary>
    /// 渡渡鸟摸头反馈。摸头检测由头部 PetZone 负责，本组件只处理渡渡鸟能否响应和反馈内容。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DodoBird))]
    public class DodoBirdPetResponder : MonoBehaviour, IPettable
    {
        [Header("状态限制")]
        [Tooltip("是否允许 Return 状态下响应摸头。默认只允许 Idle / Wait。")]
        [SerializeField] private bool allowDuringReturn;

        [Header("反馈")]
        [Tooltip("摸头成功时触发的 Animator Trigger。为空则不触发动画参数。")]
        [SerializeField] private string petAnimatorTrigger;
        [Tooltip("可选的摸头反馈粒子。")]
        [SerializeField] private ParticleSystem feedbackParticle;

        [Header("音效")]
        [Tooltip("SoundData 中的音效组名。为空则不播放。")]
        [SerializeField] private string soundGroupName;
        [Tooltip("SoundData 中的音效名。为空则不播放。")]
        [SerializeField] private string soundName;
        [SerializeField] private float soundVolume = 1f;

        [Header("调试")]
        [Tooltip("开启后，每次渡渡鸟成功响应摸头都会在 Console 输出日志。")]
        [SerializeField] private bool enableDebugLog = true;

        private DodoBird _bird;

        public bool CanBePetted
        {
            get
            {
                if (_bird == null)
                    return false;

                return _bird.CurrentStateType switch
                {
                    DodoBirdStateType.Idle => true,
                    DodoBirdStateType.Wait => true,
                    DodoBirdStateType.Return => allowDuringReturn,
                    _ => false
                };
            }
        }

        private void Awake()
        {
            _bird = GetComponent<DodoBird>();
        }

        public void OnPetted(PetContext context)
        {
            if (!CanBePetted)
                return;

            if (enableDebugLog)
            {
                string interactorName = context.Interactor != null ? context.Interactor.name : "<null>";
                Debug.Log(
                    $"[DodoBirdPetResponder] {name} 被摸头。State={_bird.CurrentStateType}, " +
                    $"Interactor={interactorName}, StrokeDistance={context.StrokeDistance:F3}, " +
                    $"HoldDuration={context.HoldDuration:F3}",
                    this);
            }

            if (!string.IsNullOrEmpty(petAnimatorTrigger))
                _bird.ani.SetTrigger(petAnimatorTrigger);

            if (feedbackParticle != null)
                feedbackParticle.Play();

            if (!string.IsNullOrEmpty(soundGroupName) && !string.IsNullOrEmpty(soundName))
                GameManager.mAudio.PlayEffect(_bird.AS, soundGroupName, soundName, soundVolume);

            this.Broadcast("Animal.Petted", this);
            this.Broadcast("DodoBird.Petted", _bird);
        }
    }
}
