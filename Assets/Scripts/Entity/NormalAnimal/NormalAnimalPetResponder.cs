using Pet;
using UnityEngine;

namespace Entity.NormalAnimal
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NormalAnimal))]
    public class NormalAnimalPetResponder : PetResponderBase<NormalAnimal>
    {
        [Header("状态限制")]
        [Tooltip("是否允许 Move 状态下响应摸头。默认允许")]
        [SerializeField] private bool allowWhileMoving = true;

        public override bool CanBePetted
        {
            get
            {
                if (m_animal == null)
                    return false;

                return m_animal.CurrentStateType switch
                {
                    NormalAnimalStateType.Idle => true,
                    NormalAnimalStateType.Move => allowWhileMoving,
                    _ => false
                };
            }
        }

        public override void OnPetBegin() => m_animal.IsPetting = true;
        public override void OnPetAfter() => m_animal.IsPetting = false;
    }
}