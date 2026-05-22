using Core.Utils;
using Core.Event;
using GiftCatch.Shot;
using Manager;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Entity.Pterosaur
{
    public class PterosaurGift : MonoBehaviour, IRigidbodyRelayReceiver, IShottable
    {
        #region SerializedFieldVariables

        [Header("Ground Gift")]
        [SerializeField] private GameObject groundMark;
        [SerializeField] private float groundBounceForce = 2.5f;
        [SerializeField] private float groundDrag = 0.8f;
        [SerializeField] private bool onlyDirectInteract = true;
        [SerializeField] private bool useCatchZoneCatch;

        [Header("Shot Lock")]
        [SerializeField] private Color lockedColor = Color.cyan;

        #endregion

        #region Properties

        private RigidbodyRelay _bodyRelay;
        private XRSimpleInteractable _it;
        private Rigidbody _rb;
        private Renderer _giftRenderer;
        private MaterialPropertyBlock _materialPropertyBlock;
        private PterosaurGiftType _type;

        #endregion

        #region Runtime

        private bool _initialized;
        private bool _caught;
        private bool _missed;
        private bool _hasBecomeGroundGift;
        private bool _isShotLocked;
        private Pterosaur _lockedPterosaur;
        private LayerMask _layerMask;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            _bodyRelay = GetComponentInChildren<RigidbodyRelay>();
            _bodyRelay.Init(this);
            _rb = GetComponentInChildren<Rigidbody>();
            _it = GetComponentInChildren<XRSimpleInteractable>();
            _giftRenderer = _bodyRelay.GetComponentInChildren<Renderer>();
            _materialPropertyBlock = new MaterialPropertyBlock();
            _it.firstHoverEntered.AddListener(OnFirstHoverEntered);
            _layerMask = LayerMask.GetMask("Land");
        }

        private void OnDestroy()
        {
            _it.firstHoverEntered.RemoveListener(OnFirstHoverEntered);
        }

        public void OnEnable()
        {
            _bodyRelay.transform.localPosition = Vector3.zero;
            _bodyRelay.transform.localRotation = Quaternion.identity;
        }

        #endregion

        #region Public Methods

        public void Initialize(
            PterosaurGiftType type,
            float airDrag,
            Vector3 initVelocity,
            bool useCatchZone = false)
        {
            _type = type;
            useCatchZoneCatch = useCatchZone;

            _initialized = true;
            _caught = false;
            _missed = false;
            _hasBecomeGroundGift = false;
            ClearShotLock();
            
            _it.enabled = !useCatchZoneCatch;
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

        public bool CanBeShotLocked => _initialized &&
                                       !_caught &&
                                       !_missed &&
                                       !_hasBecomeGroundGift &&
                                       !_isShotLocked;

        /// <summary>
        /// 判断当前礼物是否仍由指定翼龙锁定。
        /// </summary>
        public bool IsLockedByPterosaur(Pterosaur pterosaur)
        {
            return _isShotLocked && _lockedPterosaur == pterosaur;
        }

        /// <summary>
        /// 响应 Shotter 的射击命中，并请求接礼物 Controller 分配翼龙。
        /// </summary>
        public void OnShot(RaycastHit hit)
        {
            // ApplyShotLockedVisual();
            if (!CanBeShotLocked)
                return;

            this.Broadcast("Gift.Shot", this);
        }

        /// <summary>
        /// 尝试将礼物锁定给指定翼龙，并切换锁定表现。
        /// </summary>
        public bool TryLockByPterosaur(Pterosaur pterosaur)
        {
            if (pterosaur == null || !CanBeShotLocked)
                return false;

            _isShotLocked = true;
            _lockedPterosaur = pterosaur;
            ApplyShotLockedVisual();
            return true;
        }

        /// <summary>
        /// 在翼龙任务启动失败等场景下释放指定翼龙的锁定。
        /// </summary>
        public bool TryReleaseShotLock(Pterosaur pterosaur)
        {
            if (!IsLockedByPterosaur(pterosaur))
                return false;

            ClearShotLock();
            return true;
        }

        /// <summary>
        /// 由锁定翼龙抵达礼物位置后调用，结算为接住。
        /// </summary>
        public void ResolveLockedCatch(Pterosaur pterosaur)
        {
            if (!IsLockedByPterosaur(pterosaur))
                return;

            Catch();
        }

        #endregion

        #region Catch / Miss

        private void OnFirstHoverEntered(HoverEnterEventArgs args)
        {
            if (!_initialized || _caught || _missed || _hasBecomeGroundGift)
                return;

            if (useCatchZoneCatch)
                return;

            if (onlyDirectInteract && args.interactorObject.transform.GetComponent<XRDirectInteractor>() == null)
                return;

            Catch();
        }

        private void Catch()
        {
            if (!_initialized || _caught || _missed || _hasBecomeGroundGift)
                return;

            ClearShotLock();

            _caught = true;
            _it.enabled = false;
            this.Broadcast("Gift.Caught", _type);
            GameManager.Pool.Return(this);
            groundMark.SetActive(false);
        }

        public void OnRelayCollisionEnter(Collision collision)
        {
            if (!_initialized || _caught || _missed)
                return;

            if (collision.gameObject.layer != LayerMask.NameToLayer("Land"))
                return;

            ClearShotLock();

            _missed = true;
            _it.enabled = false;
            this.Broadcast("Gift.Missed", _type);
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

        private void ApplyShotLockedVisual()
        {
            if (_giftRenderer == null)
                return;

            _giftRenderer.GetPropertyBlock(_materialPropertyBlock);
            _materialPropertyBlock.SetColor("_BaseColor", lockedColor);
            _materialPropertyBlock.SetColor("_Color", lockedColor);
            _giftRenderer.SetPropertyBlock(_materialPropertyBlock);
        }

        private void ClearShotLock()
        {
            _isShotLocked = false;
            _lockedPterosaur = null;

            if (_giftRenderer == null || _materialPropertyBlock == null)
                return;

            _materialPropertyBlock.Clear();
            _giftRenderer.SetPropertyBlock(_materialPropertyBlock);
        }

        public void OnRelayCollisionExit(Collision collision)
        {
        }

        public void OnRelayTriggerEnter(Collider other)
        {
            if (!_initialized || _caught || _missed || _hasBecomeGroundGift)
                return;

            if (!useCatchZoneCatch)
                return;

            if (other.CompareTag("Player"))
                Catch();
        }

        public void OnRelayTriggerExit(Collider other)
        {
        }
        
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (_hasBecomeGroundGift)
                return;
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
