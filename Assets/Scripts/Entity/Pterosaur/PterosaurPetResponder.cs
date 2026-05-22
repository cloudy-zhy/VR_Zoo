using Core.Event;
using Pet;
using UnityEngine;

namespace Entity.Pterosaur
{
    /// <summary>
    /// 翼龙摸头反馈。适合配合头部子节点上的 PetZone 使用。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Pterosaur))]
    public class PterosaurPetResponder : PetResponderBase<Pterosaur>
    {
        [Header("状态限制")]
        [Tooltip("是否允许 Move 状态下响应摸头。默认只允许 Idle。")]
        [SerializeField] private bool allowWhileMoving;

        public override bool CanBePetted
        {
            get
            {
                if (m_animal == null)
                    return false;

                return m_animal.CurrentStateType switch
                {
                    PterosaurStateType.Idle => true,
                    PterosaurStateType.Move => allowWhileMoving,
                    _ => false
                };
            }
        }

        protected override void BroadcastPetEvent()
        {
            this.Broadcast("Animal.Petted", this);
            this.Broadcast("Pterosaur.Petted", m_animal);
        }
    }
}
