using System;
using Core.Event;
using Core.Fsm;
using Entity.Pterosaur.State;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit;
using Random = UnityEngine.Random;

namespace Entity.Pterosaur
{
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class Pterosaur : MonoBehaviour, IAnimator, IAudioSource, ICurStateType<PterosaurStateType>
    {
        #region Components
        
        public NavMeshAgent nav { get; private set; }
        public Animator ani { get; private set; }
        public XRSimpleInteractable xri { get; private set; }
        public AudioSource aus { get; private set; }
        
        #endregion
        
        #region StateMachineVariables

        // 是否到达指定地点
        public bool IsReached
        {
            get
            {
                Vector3 offset = transform.position - Destination;
                offset.y = 0;
                return offset.sqrMagnitude <= 0.25f;
            }
        }
        public bool IsHovered { get; private set; }
        // public bool IsArrived { get; private set; }
        public bool IsCalled  { get; private set; }
        public bool HadDestination { get; private set; }
        public bool HadRequest => _remainReqs != 0;

        public Vector3 Destination { get; private set; }
        public PterosaurGift GiftCatchTarget { get; private set; }
        public Vector3 GiftCatchReturnPosition { get; private set; }
        public float GiftChaseSpeed => giftChaseSpeed;
        public float GiftCatchReturnSpeed => giftCatchReturnSpeed;
        public float GiftCatchDistance => giftCatchDistance;
        public float GiftCatchReturnDistance => giftCatchReturnDistance;
        public bool HasGiftCatchTask => GiftCatchTarget != null ||
                                        CurrentStateType == PterosaurStateType.GiftChase ||
                                        CurrentStateType == PterosaurStateType.ReturnToPlayer;

        
        #endregion
        
        #region PrivateVariables
        
        private StateMachine<PterosaurStateType> _fsm;
        private int _remainReqs;
        private bool _isFsmInitialized;
        public PterosaurStateType CurrentStateType => _fsm != null ? _fsm.CurrentKey : PterosaurStateType.Idle;
        Enum ICurStateType.CurrentStateTypeEnum => CurrentStateType;

        #endregion

        #region SerializeFieldVariables

        [Header("随机飞行")]
        [Tooltip("随机飞行选取半径")]
        [SerializeField] private float randomRadius = 20f;
        [Tooltip("随机选取最大次数，次数过小可能导致选取失败，不进行随机飞行")]
        [SerializeField] private int randomRetryTimes = 20;

        [Header("接礼物")]
        [SerializeField] private float giftChaseSpeed = 6f;
        [SerializeField] private float giftCatchReturnSpeed = 5f;
        [SerializeField] private float giftCatchRotateSpeed = 540f;
        [SerializeField] private float giftCatchDistance = 0.35f;
        [SerializeField] private float giftCatchReturnDistance = 0.4f;
        [SerializeField] private float giftCatchReturnRadius = 2f;

        #endregion
        
        #region XRStateMethod

        private void RegisterHoverEnter(HoverEnterEventArgs args)
        {
            IsHovered = true;
        }

        private void RegisterHoverExit(HoverExitEventArgs args)
        {
            IsHovered = false;
        }
        
        #endregion
        
        #region LifeCycle

        private void Awake()
        {
            FetchComponents();
            BuildFsm();
        }

        private void OnDestroy()
        {
            UnregisterXR();
        }

        private void Start()
        {
            RegisterXR();
            _fsm.Initialize(PterosaurStateType.Idle);
            _isFsmInitialized = true;
            SetRandomDestination();
        }
        
        private void Update()       => _fsm.OnUpdate();

        #endregion
        
        #region PrivateMethods

        private void FetchComponents()
        {
            nav = GetComponent<NavMeshAgent>();
            ani = GetComponent<Animator>();
            xri = GetComponent<XRSimpleInteractable>();
            aus = GetComponent<AudioSource>();
        }

        private void BuildFsm()
        {
            _fsm = new StateMachine<PterosaurStateType>();
            _fsm.AddState(PterosaurStateType.Idle, new IdleState(this, _fsm, "Idle"));
            _fsm.AddState(PterosaurStateType.Move, new MoveState(this, _fsm, "Move"));
            _fsm.AddState(PterosaurStateType.Throw, new ThrowState(this, _fsm, "Throw"));
            _fsm.AddState(PterosaurStateType.GiftChase, new GiftChaseState(this, _fsm, "Move"));
            _fsm.AddState(PterosaurStateType.ReturnToPlayer, new ReturnToPlayerState(this, _fsm, "Move"));
            // _fsm.OnStateChanged += (from, to) =>
            //     Debug.Log($"[Pterosaur:{name}] {from} → {to}");
        }

        private void RegisterXR()
        {
            xri.hoverEntered.AddListener(RegisterHoverEnter);
            xri.hoverExited.AddListener(RegisterHoverExit);
        }

        private void UnregisterXR()
        {
            xri.hoverEntered.RemoveListener(RegisterHoverEnter);
            xri.hoverExited.RemoveListener(RegisterHoverExit);
        }
        
        #endregion

        #region PublicMethods

        public void SetRandomDestination()
        {
            HadDestination = false;
            for (int i = 0; i < randomRetryTimes; i++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * randomRadius;
                Vector3 random = transform.position + new Vector3(
                    randomCircle.x,
                    0f,
                    randomCircle.y
                );

                if (NavMesh.SamplePosition(random, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    HadDestination = true;
                    Destination = hit.position;
                    return;
                }
            }
        }

        public void CallDown()
        {
            IsCalled = true;
        }

        public void AddRequest()
        {
            _remainReqs += 1;
        }

        public void CutRequest()
        {
            _remainReqs = Math.Max(0, _remainReqs - 1);
        }

        /// <summary>
        /// 尝试让翼龙进入射击锁定礼物的追取任务。
        /// </summary>
        public bool TryStartGiftCatchTask(PterosaurGift gift)
        {
            if (gift == null || _fsm == null || !_isFsmInitialized || HasGiftCatchTask)
                return false;

            GiftCatchTarget = gift;
            _fsm.ChangeState(PterosaurStateType.GiftChase);
            return true;
        }

        /// <summary>
        /// 清理当前射击接礼物任务目标。
        /// </summary>
        public void ClearGiftCatchTask()
        {
            GiftCatchTarget = null;
        }

        /// <summary>
        /// 生成玩家同高度附近的任务返回点。
        /// </summary>
        public void CreateGiftCatchReturnPosition()
        {
            Transform player = Camera.main != null ? Camera.main.transform : transform;
            Vector2 offset = Random.insideUnitCircle * giftCatchReturnRadius;
            Vector3 playerPosition = player.position;

            GiftCatchReturnPosition = new Vector3(
                playerPosition.x + offset.x,
                playerPosition.y,
                playerPosition.z + offset.y
            );
        }

        /// <summary>
        /// 用非 NavMesh 的方式直接朝目标点飞行。
        /// </summary>
        public void MoveDirectlyTowards(Vector3 targetPosition, float speed)
        {
            Transform self = transform;
            Vector3 currentPosition = self.position;
            Vector3 direction = targetPosition - currentPosition;

            self.position = Vector3.MoveTowards(
                currentPosition,
                targetPosition,
                speed * Time.deltaTime
            );

            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            self.rotation = Quaternion.RotateTowards(
                self.rotation,
                targetRotation,
                giftCatchRotateSpeed * Time.deltaTime
            );
        }

        /// <summary>
        /// 判断翼龙是否已经进入目标点指定半径。
        /// </summary>
        public bool IsNearPosition(Vector3 position, float distance)
        {
            return (transform.position - position).sqrMagnitude <= distance * distance;
        }

        /// <summary>
        /// 通知射击接礼物 Controller 该翼龙已回到可用位置。
        /// </summary>
        public void BroadcastGiftCatchReturn()
        {
            this.Broadcast("Pterosaur.GiftCatchReturn", this);
        }

        #endregion
        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Destination, 0.15f);
        }
#endif
        
    }
}
