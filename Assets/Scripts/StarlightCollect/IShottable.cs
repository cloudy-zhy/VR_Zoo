using UnityEngine;

namespace StarlightCollect
{
    /// <summary>
    /// 可响应星光法杖射击命中的对象接口。
    /// </summary>
    public interface IShottable
    {
        /// <summary>
        /// 响应一次有效射击命中。
        /// </summary>
        void OnShot(RaycastHit hit);
    }
}
