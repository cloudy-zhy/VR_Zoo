using UnityEngine;

namespace Core.Pool
{
    /// <summary>
    /// 表示调用方是否显式指定了池对象租借时的父级。
    /// </summary>
    public readonly struct PoolParentOverride
    {
        /// <summary>
        /// 调用方是否传入了 parent 参数。显式传入 null 时该值也为 true。
        /// </summary>
        public bool IsSpecified { get; }

        /// <summary>
        /// 调用方指定的父级；可以为 null，表示脱离父级到当前场景根节点。
        /// </summary>
        public Transform Value { get; }

        private PoolParentOverride(Transform value)
        {
            IsSpecified = true;
            Value = value;
        }

        /// <summary>
        /// 将 Transform 转换为显式父级覆盖；value 可以为 null。
        /// </summary>
        public static implicit operator PoolParentOverride(Transform value) => new(value);
    }
}
