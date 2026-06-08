using UnityEngine;

namespace Pet
{
    /// <summary>
    /// 可响应摸头交互的对象接口。
    /// </summary>
    public interface IPettable
    {
        /// <summary>
        /// 当前是否允许响应摸头。
        /// </summary>
        bool CanBePetted { get; }

        /// <summary>
        /// 响应一次有效摸头。
        /// </summary>
        void OnPetted(PetContext context);
        
        void OnPetBegin();
        void OnPetAfter();
    }
}
