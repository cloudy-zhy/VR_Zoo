using Core.Event;
using Manager;
using UnityEngine;

namespace Slingshot
{
    [RequireComponent(typeof(Rigidbody))]
    public class SlingshotFruit : MonoBehaviour
    {
        [SerializeField] private SlingshotFruitType slingshotFruitType;

        private static int _landLayer;
        private Rigidbody _rb;
        private bool _isFalling;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.isKinematic = true;
            _landLayer = LayerMask.NameToLayer("Land");
        }

        private void OnCollisionEnter(Collision collision)
        {
            // 防止意外碰撞
            if (!_isFalling && 
                (collision.gameObject.CompareTag("DodoBird") ||
                 collision.gameObject.CompareTag("Fruit")))
            {
                _isFalling = true;
                _rb.isKinematic = false;
                _rb.useGravity = true;
                // 广播消息被撞到了，只有在树上的有资格被撞
                GameManager.Event.Broadcast("DodoBird.HitFruit", slingshotFruitType);
                GameManager.Event.Broadcast("DodoBird.FruitHit", "DodoBird.FruitHit");
                GetComponent<AudioSource>().Play();
                return;
            }

            if (collision.gameObject.layer == _landLayer)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
                GetComponent<AudioSource>().Play();
            }
            // else if(collision.gameObject.CompareTag("Fruit"))
            // {
            //     // 果子撞到果子了
            //     GameManager.Event.Broadcast("DodoBird.HitFruit", 
            //         new EventParameter<SlingshotFruitType>(slingshotFruitType));
            // }
        }
    }
}