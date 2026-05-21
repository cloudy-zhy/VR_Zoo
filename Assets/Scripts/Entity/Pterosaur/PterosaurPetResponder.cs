using Core.Event;
using Manager;
using Pet;
using UnityEngine;

namespace Entity.Pterosaur
{
    /// <summary>
    /// 翼龙摸头反馈。适合配合头部子节点上的 PetZone 使用。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Pterosaur))]
    public class PterosaurPetResponder : MonoBehaviour, IPettable
    {
        [Header("状态限制")]
        [Tooltip("是否允许 Move 状态下响应摸头。默认只允许 Idle。")]
        [SerializeField] private bool allowWhileMoving;

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

        private Pterosaur _pterosaur;

        public bool CanBePetted
        {
            get
            {
                if (_pterosaur == null)
                    return false;

                return _pterosaur.CurrentStateType switch
                {
                    PterosaurStateType.Idle => true,
                    PterosaurStateType.Move => allowWhileMoving,
                    _ => false
                };
            }
        }

        private void Awake()
        {
            _pterosaur = GetComponent<Pterosaur>();
        }

        public void OnPetted(PetContext context)
        {
            if (!CanBePetted)
                return;

            if (_pterosaur == null)
                return;

            if (!string.IsNullOrEmpty(petAnimatorTrigger))
                _pterosaur.ani.SetTrigger(petAnimatorTrigger);

            if (feedbackParticle != null)
                feedbackParticle.Play();

            if (!string.IsNullOrEmpty(soundGroupName) && !string.IsNullOrEmpty(soundName))
                GameManager.mAudio.PlayEffect(_pterosaur.aus, soundGroupName, soundName, soundVolume);

            this.Broadcast("Animal.Petted", this);
            this.Broadcast("Pterosaur.Petted", _pterosaur);
        }
    }
}
