using Core.Event;
using Manager;
using UnityEngine;

namespace StarlightCollect
{
    /// <summary>
    /// 星光能量，Collect链路
    /// 1.翼龙到达位点后，广播消息，CatchCon接收后申请创建并调用Initialize
    /// 2.不断Update中slerp直到到达Lantern附近
    /// 3.到达将被收集，广播到达，回归对象池，TODO：播放粒子、音效
    /// </summary>
    public class StarlightCollecting : MonoBehaviour
    {
        [SerializeField] private float duration = 2f;
        [SerializeField] private float curveHeight = 2f;

        private bool _isMoving;
        private float _elapsedTime;
        private float _arrivalDistSqr;
        private Vector3 _startPosition;
        private Transform _targetTran;
        
        public void Initialize(float arrivalDistSqr, Transform targetTran)
        {
            _arrivalDistSqr = arrivalDistSqr;
            _targetTran = targetTran;
            _startPosition = transform.position;
            _isMoving = true;
            _elapsedTime = 0f;
        }

        private void Update()
        {
            if (!_isMoving) return;

            if (_targetTran == null)
            {
                _isMoving = false;
                GameManager.Pool.Return(gameObject);
                return;
            }
            
            _elapsedTime += Time.deltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(_elapsedTime / duration);
            Vector3 targetPosition = _targetTran.position;
            Vector3 controlPoint = (_startPosition + targetPosition) * 0.5f + Vector3.up * (curveHeight * 2f);
            Vector3 startToControl = Vector3.Lerp(_startPosition, controlPoint, t);
            Vector3 controlToEnd = Vector3.Lerp(controlPoint, targetPosition, t);
            transform.position = Vector3.Lerp(startToControl, controlToEnd, t);

            float distSqr = (targetPosition - transform.position).sqrMagnitude;
            if (t >= 1f || distSqr < _arrivalDistSqr)
            {
                _isMoving = false;
                Collected();
            }
        }
        
        private void Collected()
        {
            this.Broadcast(StarlightConstant.StarLightCollected);
            GameManager.Pool.Return(gameObject);
        }
    }
}
