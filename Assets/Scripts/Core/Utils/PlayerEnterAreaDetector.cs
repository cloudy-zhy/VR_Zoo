using System;
using UnityEngine;

namespace Core.Utils
{
    public class PlayerEnterAreaDetector : MonoBehaviour
    {
        private Transform _playerTrans;
        private Collider _areaCollider;
        private float _epsilon = 0.001f;
        private bool _isInside;
        
        public event Action OnPlayerEnterArea;
        public event Action OnPlayerExitArea;

        private void Awake()
        {
            _areaCollider = GetComponent<Collider>();
            _playerTrans = Camera.main?.transform;
        }
        
        private void Update()
        {
            if (!_playerTrans || !_areaCollider) return;

            Vector3 closest = _areaCollider.ClosestPoint(_playerTrans.position);
            bool inside = (closest - _playerTrans.position).sqrMagnitude <= _epsilon * _epsilon;

            if (inside == _isInside) return;

            _isInside = inside;

            if (_isInside)
            {
                OnPlayerEnterArea?.Invoke();
            }
            else
                OnPlayerExitArea?.Invoke();
        }
    }
}