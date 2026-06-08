using System;
using Core.Event;
using Manager;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace StarlightCollect
{
    /// <summary>
    /// 星光能量，Throw-Catch链路
    /// 1.当被翼龙释放时，调用Initialize，赋予初始速度
    /// 2.当被射中时，停止运动，并通知
    /// 3.当被翼龙catch时，回归对象池
    /// 4.当撞到地面时，回归对象池，并申请消散特效于原地
    /// TODO：(BUG)当可用翼龙数目不足时，星光会保持定在空中，无法被消除
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class StarLight : MonoBehaviour, IShottable
    {
        [SerializeField] private LayerMask landLayer;
        
        private Rigidbody _rigidbody;
        public bool IsShotLocked { get; private set; }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.useGravity = false;
        }

        public void Initialize(float initialVelocity)
        {
            IsShotLocked = false;
            _rigidbody.isKinematic = false;
            _rigidbody.velocity = transform.up * -initialVelocity;
        }

        public void OnShot(RaycastHit hit)
        {
            if (IsShotLocked) return;
            this.Broadcast(StarlightConstant.StarlightMarked, this);
        }

        public void Locked()
        {
            IsShotLocked = true;
            _rigidbody.isKinematic = true;
            OnShotVisual();
        }

        private void OnShotVisual()
        {
            // TODO:被定住后的视觉效果
        }

        private void OnTriggerExit(Collider other)
        {
            if (landLayer.Contains(other.gameObject.layer))
            {
                // print("Land!");
                GameManager.Pool.Return(gameObject);
                GameManager.Pool.Rent(StarlightConstant.DisappearPoPoolKey, position : transform.position);
            }
        }
    }
}
