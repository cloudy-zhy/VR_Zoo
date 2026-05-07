using Core.Event;
using Core.Pool;
using Core.Utils;
using Cysharp.Threading.Tasks;
using Manager;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Entity.Pterosaur
{
    public class PterosaurGift : PoolableObject, IRigidbodyRelayReceiver
    {
        #region SerializedFieldVariables

        [Header("Ground Gift")]
        [SerializeField] private GameObject groundMark;
        [SerializeField] private float groundBounceForce = 2.5f;
        [SerializeField] private float groundDrag = 0.8f;
        [SerializeField] private bool onlyDirectInteract = true;

        #endregion

        #region Properties

        private RigidbodyRelay _bodyRelay;
        private XRSimpleInteractable _it;
        private Rigidbody _rb;
        private PterosaurGiftType _type;

        #endregion

        #region Runtime

        private bool _initialized;
        private bool _caught;
        private bool _missed;
        private bool _hasBecomeGroundGift;
        private LayerMask _layerMask;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            _bodyRelay = GetComponentInChildren<RigidbodyRelay>();
            _bodyRelay.Init(this);
            _rb = GetComponentInChildren<Rigidbody>();
            _it = GetComponentInChildren<XRSimpleInteractable>();
            _it.firstHoverEntered.AddListener(OnFirstHoverEntered);
            _layerMask = LayerMask.GetMask("Land");
        }

        private void OnDestroy()
        {
            _it.firstHoverEntered.RemoveListener(OnFirstHoverEntered);
        }

        public override void OnSpawnFromPool()
        {
            base.OnSpawnFromPool();
            _bodyRelay.transform.localPosition = Vector3.zero;
            _bodyRelay.transform.localRotation = Quaternion.identity;
        }

        #endregion

        #region Public Methods

        public void Initialize(PterosaurGiftType type, float airDrag, Vector3 initVelocity)
        {
            _type = type;

            _initialized = true;
            _caught = false;
            _missed = false;
            _hasBecomeGroundGift = false;
            
            _it.enabled = true;
            _rb.isKinematic = false;
            _rb.drag = airDrag;
            _rb.velocity = initVelocity;
            
            ApplyVisualByType(_type);
            
            Vector3 origin = transform.position + Vector3.down * 2f;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 50f, _layerMask))
            {
                groundMark.transform.position = hit.point + hit.normal * 0.02f;
                groundMark.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                groundMark.SetActive(true);
            }
            else
            {
                groundMark.SetActive(false);
            }
        }

        #endregion

        #region Catch / Miss

        private void OnFirstHoverEntered(HoverEnterEventArgs args)
        {
            if (!_initialized || _caught || _missed || _hasBecomeGroundGift)
                return;

            if (_caught || _missed)
                return;

            if (onlyDirectInteract && args.interactorObject.transform.GetComponent<XRDirectInteractor>() == null)
                return;

            _caught = true;
            _it.enabled = false;
            GameManager.Event.Broadcast("Gift.Caught", _type);
            PoolManager.I.Return(this);
            groundMark.SetActive(false);
        }

        public void OnRelayCollisionEnter(Collision collision)
        {
            if (!_initialized || _caught || _missed)
                return;

            if (collision.gameObject.layer != LayerMask.NameToLayer("Land"))
                return;

            _missed = true;
            _it.enabled = false;
            GameManager.Event.Broadcast("Gift.Missed", _type);
            BecomeGroundGift();
            groundMark.SetActive(false);
        }

        #endregion

        private void BecomeGroundGift()
        {
            _hasBecomeGroundGift = true;

            _rb.isKinematic = false;
            _rb.drag = groundDrag;

            Vector3 bounceDirection = new Vector3(
                Random.Range(-0.4f, 0.4f),
                1f,
                Random.Range(-0.4f, 0.4f)
            ).normalized;

            _rb.AddForce(bounceDirection * groundBounceForce, ForceMode.Impulse);
        }

        private void ApplyVisualByType(PterosaurGiftType type)
        {
            switch (type)
            {
                case PterosaurGiftType.Tutorial:
                    // TODO: 红色拖尾、明显高光
                    break;

                case PterosaurGiftType.Lucky:
                    // TODO: 金色高光
                    break;

                case PterosaurGiftType.Fast:
                    // TODO: 快速包裹高光拖尾
                    break;

                case PterosaurGiftType.Rainbow:
                    // TODO: 彩虹材质、强拖尾
                    break;

                case PterosaurGiftType.Normal:
                default:
                    break;
            }
        }

        public void OnRelayCollisionExit(Collision collision)
        {
        }

        public void OnRelayTriggerEnter(Collider other)
        {
        }

        public void OnRelayTriggerExit(Collider other)
        {
        }
        
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position + Vector3.down * 2f;
            Vector3 rayEnd = origin + Vector3.down * 50f;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(origin, rayEnd);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 50f, _layerMask))
            {
                Gizmos.DrawSphere(hit.point, 0.15f);
            }
        }
#endif
        
    }
}