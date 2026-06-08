using Core.Fsm;
using Manager;
using UnityEngine;

namespace Pet
{
    /// <summary>
    /// 动物摸头响应基类，统一处理反馈播放、通用事件广播和调试输出。
    /// </summary>
    public abstract class PetResponderBase<TAnimal> : MonoBehaviour, IPettable
        where TAnimal : Component, IAnimator, IAudioSource, ICurStateType
    {
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
        [Tooltip("开启后，每次成功响应摸头都会在 Console 输出日志。")]
        [SerializeField] private bool enableDebugLog = true;

        public virtual bool CanBePetted => false;
        protected TAnimal m_animal;


        public void Awake()
        {
            m_animal = GetComponentInParent<TAnimal>();
        }

        public void OnPetted(PetContext context)
        {
            if (!CanBePetted)
                return;

            if (enableDebugLog)
            {
                string interactorName = context.Interactor != null ? context.Interactor.name : "<null>";
                Debug.Log(
                    $"[DodoBirdPetResponder] {name} 被摸头。State={m_animal.CurrentStateTypeEnum}, " +
                    $"Interactor={interactorName}, StrokeDistance={context.StrokeDistance:F3}, " +
                    $"HoldDuration={context.HoldDuration:F3}",
                    this);
            }

            if (!string.IsNullOrEmpty(petAnimatorTrigger))
                m_animal.ani.SetTrigger(petAnimatorTrigger);

            if (feedbackParticle != null)
                feedbackParticle.Play();

            if (!string.IsNullOrEmpty(soundGroupName) && !string.IsNullOrEmpty(soundName))
                GameManager.mAudio.PlayEffect(m_animal.aus, soundGroupName, soundName, soundVolume);

            BroadcastPetEvent();
        }

        public virtual void OnPetBegin() { }

        public virtual void OnPetAfter() { }

        protected virtual void BroadcastPetEvent() { }
    }
}
