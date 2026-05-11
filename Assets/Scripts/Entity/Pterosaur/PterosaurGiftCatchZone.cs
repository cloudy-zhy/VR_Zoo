using UnityEngine;

namespace Entity.Pterosaur
{
    public class PterosaurGiftCatchZone : MonoBehaviour
    {
        // 因为相机自己单独动，但是cameraOffset不动，只能单独移动区域的x和z
        private Transform _player;

        private void Awake()
        {
            _player = Camera.main.transform;
        }
        private void LateUpdate()
        {
            Vector3 playerPos = _player.position;
            playerPos.y = transform.position.y;
            transform.position = playerPos;
        }
    }
}