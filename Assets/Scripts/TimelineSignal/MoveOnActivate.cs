using UnityEngine;

namespace TimelineSignal
{
    /// <summary>
    /// 挂到场景中任意有 PlayableDirector 的 GameObject 上，
    /// 配合 Timeline Signal Track 使用：在 15 秒处发射 Signal，
    /// 在 Signal Receiver 中绑定 MoveToTarget() 方法。
    /// </summary>
    public class MoveOnActivate : MonoBehaviour
    {
        [Header("移动目标")]
        [Tooltip("需要移动的物体（不填则移动自身）")]
        [SerializeField] private Transform targetObject;

        [Header("目标位置")]
        [Tooltip("15 秒后移动到的目标位置")]
        [SerializeField] private Vector3 targetPosition;

        /// <summary>
        /// 供 Timeline Signal 调用的无参方法。
        /// </summary>
        public void MoveToTarget()
        {
            Transform t = targetObject != null ? targetObject : transform;
            t.position = targetPosition;
        }
    }
}
