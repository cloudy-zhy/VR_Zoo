using Core.Event;
using Pet;
using UnityEngine;

namespace Entity.DodoBird
{
    /// <summary>
    /// 渡渡鸟摸头反馈。摸头检测由头部 PetZone 负责，本组件只处理渡渡鸟能否响应和反馈内容。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DodoBird))]
    public class DodoBirdPetResponder : PetResponderBase<DodoBird>
    {
        [Header("状态限制")]
        [Tooltip("是否允许 Return 状态下响应摸头。默认只允许 Idle / Wait。")]
        [SerializeField] private bool allowDuringReturn;

        public override bool CanBePetted
        {
            get
            {
                if (m_animal == null)
                    return false;

                return m_animal.CurrentStateType switch
                {
                    DodoBirdStateType.Idle => true,
                    DodoBirdStateType.Wait => true,
                    DodoBirdStateType.Return => allowDuringReturn,
                    _ => false
                };
            }
        }

        protected override void BroadcastPetEvent()
        {
            this.Broadcast("Animal.Petted", this);
            this.Broadcast("DodoBird.Petted", m_animal);
        }
    }
}
