using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Pet
{
    /// <summary>
    /// 通用摸头检测区。依赖 XRI hover 生命周期，只负责判定，不负责具体动物反馈。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class PetZone : MonoBehaviour
    {
        [Header("目标")]
        [Tooltip("可选。指定后从该节点向父级查找 IPettable；为空则从当前节点向父级查找。")]
        [SerializeField] private Transform pettableRoot;

        [Header("交互过滤")]
        [Tooltip("开启后只允许 XRDirectInteractor 触发，避免射线远距离误触摸头。")]
        [SerializeField] private bool onlyDirectInteractor = true;

        [Header("摸头判定")]
        [Tooltip("手或控制器在区域内累计移动超过该距离后，视为一次有效摸头。")]
        [SerializeField] private float minStrokeDistance = 0.12f;
        [Tooltip("进入区域后至少停留多久才允许触发。")]
        [SerializeField] private float minHoverDuration = 0.15f;
        [Tooltip("同一 PetZone 两次触发之间的最小间隔。")]
        [SerializeField] private float cooldown = 0.8f;
        [Tooltip("开启后，同一次 hover 期间最多触发一次。默认关闭，让 cooldown 控制同一次接触内的重复摸头。")]
        [SerializeField] private bool triggerOncePerHover;

        private XRSimpleInteractable _interactable;
        private Collider _collider;
        private IPettable _pettable;
        private Transform _activeInteractor;
        private GameObject _activeInteractorObject;
        private Vector3 _lastInteractorPosition;
        private float _strokeDistance;
        private float _hoverStartTime;
        private float _lastPetTime = -999f;
        private bool _hasTriggeredInCurrentHover;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _interactable = GetComponent<XRSimpleInteractable>();
            CachePettable();
        }

        private void OnEnable()
        {
            _interactable.hoverEntered.AddListener(OnHoverEntered);
            _interactable.hoverExited.AddListener(OnHoverExited);
        }

        private void OnDisable()
        {
            _interactable.hoverEntered.RemoveListener(OnHoverEntered);
            _interactable.hoverExited.RemoveListener(OnHoverExited);
            ClearHover();
        }

        private void Update()
        {
            if (_activeInteractor == null)
                return;

            Vector3 currentPosition = _activeInteractor.position;
            _strokeDistance += Vector3.Distance(_lastInteractorPosition, currentPosition);
            _lastInteractorPosition = currentPosition;

            if (!CanTrigger())
                return;

            TriggerPet(currentPosition);
        }

        private void Reset()
        {
            Collider zoneCollider = GetComponent<Collider>();
            zoneCollider.isTrigger = false;
        }

        private void OnValidate()
        {
            minStrokeDistance = Mathf.Max(0f, minStrokeDistance);
            minHoverDuration = Mathf.Max(0f, minHoverDuration);
            cooldown = Mathf.Max(0f, cooldown);

            if (_collider == null)
                _collider = GetComponent<Collider>();
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (_activeInteractor != null)
                return;

            Transform interactorTransform = args.interactorObject.transform;
            if (onlyDirectInteractor && interactorTransform.GetComponent<XRDirectInteractor>() == null)
                return;

            //CachePettable();
            _activeInteractor = interactorTransform;
            _activeInteractorObject = interactorTransform.gameObject;
            _lastInteractorPosition = interactorTransform.position;
            _strokeDistance = 0f;
            _hoverStartTime = Time.time;
            _hasTriggeredInCurrentHover = false;
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            if (_activeInteractor == args.interactorObject.transform)
                ClearHover();
        }

        private bool CanTrigger()
        {
            if (_pettable == null || !_pettable.CanBePetted)
                return false;

            if (triggerOncePerHover && _hasTriggeredInCurrentHover)
                return false;

            if (Time.time - _lastPetTime < cooldown)
                return false;

            if (Time.time - _hoverStartTime < minHoverDuration)
                return false;

            return _strokeDistance >= minStrokeDistance;
        }

        private void TriggerPet(Vector3 contactPosition)
        {

            PetContext context = new PetContext(
                _activeInteractorObject,
                transform,
                contactPosition,
                _strokeDistance,
                Time.time - _hoverStartTime);

            if (!_hasTriggeredInCurrentHover)
                _pettable.OnPetBegin();
            _pettable.OnPetted(context);

            _strokeDistance = 0f;
            _lastPetTime = Time.time;
            _hoverStartTime = Time.time;
            _lastInteractorPosition = contactPosition;
            _hasTriggeredInCurrentHover = true;
        }

        private void ClearHover()
        {
            if (_hasTriggeredInCurrentHover)
                _pettable.OnPetBegin();
            _activeInteractor = null;
            _activeInteractorObject = null;
            _strokeDistance = 0f;
            _hoverStartTime = 0f;
            _hasTriggeredInCurrentHover = false;
        }

        private void CachePettable()
        {
            Transform root = pettableRoot != null ? pettableRoot : transform;
            MonoBehaviour[] behaviours = root.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPettable pettable)
                {
                    _pettable = pettable;
                    return;
                }
            }

            _pettable = null;
        }
    }
}
