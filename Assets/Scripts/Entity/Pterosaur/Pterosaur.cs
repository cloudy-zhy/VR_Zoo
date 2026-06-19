using System;
using Core.Event;
using Core.Fsm;
using Core.Utils;
using Entity.Pterosaur.State;
using Manager;
using StarlightCollect;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace Entity.Pterosaur
{
    [DisallowMultipleComponent]
    public class Pterosaur : MonoBehaviour, IAnimator, IAudioSource, ICurStateType<PterosaurStateType>
    {
        public bool autoMove = true;
        
        #region Components
        
        public NavMeshAgent nav { get; private set; }
        public Animator ani { get; private set; }
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
        // public bool IsArrived { get; private set; }
        public bool IsCalled  { get; private set; }
        public bool HadDestination { get; private set; }
        public bool HadRequest => _remainReqs != 0;

        public Vector3 Destination { get; private set; }
        public StarLight StarLightCatchTarget { get; private set; }
        public Vector3 StarLightReturnPosition { get; private set; }
        public float StarLightChaseSpeed => starLightChaseSpeed;
        public float StarLightReturnSpeed => starLightReturnSpeed;
        public float StarLightCatchDistance => starLightCatchDistance;
        public float StarLightReturnDistance => starLightReturnDistance;
        public bool IsCarryingStarLight { get; private set; }
        public bool HasStarLightTask => StarLightCatchTarget != null ||
                                        IsCarryingStarLight ||
                                        CurrentStateType == PterosaurStateType.Chase ||
                                        CurrentStateType == PterosaurStateType.Return;

        
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

        [Header("接星光")]
        [FormerlySerializedAs("giftChaseSpeed")]
        [SerializeField] private float starLightChaseSpeed = 6f;
        [FormerlySerializedAs("giftCatchReturnSpeed")]
        [SerializeField] private float starLightReturnSpeed = 5f;
        [FormerlySerializedAs("giftCatchRotateSpeed")]
        [SerializeField] private float starLightCatchRotateSpeed = 540f;
        [FormerlySerializedAs("giftCatchDistance")]
        [SerializeField] private float starLightCatchDistance = 0.35f;
        [FormerlySerializedAs("giftCatchReturnDistance")]
        [SerializeField] private float starLightReturnDistance = 0.4f;
        [FormerlySerializedAs("giftCatchReturnRadius")]
        [SerializeField] private float starLightReturnRadius = 2f;

        #endregion
        
        #region LifeCycle

        private void Awake()
        {
            FetchComponents();
            BuildFsm();
        }

        private void Start()
        {
            _fsm.Initialize(PterosaurStateType.Idle);
            _isFsmInitialized = true;
            if (autoMove)
                SetRandomDestination();
            GameManager.Event.Register(StarlightConstant.GameEnd, OnGameEnd);
        }

        private void OnDestroy()
        {
            GameManager.Event.Unregister(StarlightConstant.GameEnd, OnGameEnd);
        }

        private void OnGameEnd(EventContext context)
        {
            _remainReqs = 0;
        }
        
        private void Update()       => _fsm.OnUpdate();

        #endregion
        
        #region PrivateMethods

        private void FetchComponents()
        {
            nav = GetComponent<NavMeshAgent>();
            ani = GetComponent<Animator>();
            aus = GetComponent<AudioSource>();
        }

        private void BuildFsm()
        {
            _fsm = new StateMachine<PterosaurStateType>();
            _fsm.AddState(PterosaurStateType.Idle, new IdleState(this, _fsm, "Idle"));
            _fsm.AddState(PterosaurStateType.Move, new MoveState(this, _fsm, "Move"));
            _fsm.AddState(PterosaurStateType.Throw, new ThrowState(this, _fsm, "Throw"));
            _fsm.AddState(PterosaurStateType.Chase, new StarLightChaseState(this, _fsm, "Fly"));
            _fsm.AddState(PterosaurStateType.Return, new ReturnToPlayerState(this, _fsm, "Fly"));
            // _fsm.OnStateChanged += (from, to) =>
            //     Debug.Log($"[Pterosaur:{name}] {from} → {to}");
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
        /// 尝试让翼龙进入星光追取任务。
        /// </summary>
        public bool TryStartStarLightCatchTask(StarLight starLight)
        {
            if (starLight.IsNull() || _fsm == null || !_isFsmInitialized || HasStarLightTask)
                return false;

            StarLightCatchTarget = starLight;
            IsCarryingStarLight = false;
            _fsm.ChangeState(PterosaurStateType.Chase);
            return true;
        }

        /// <summary>
        /// 标记翼龙已成功抓取星光，后续回到玩家附近投递。
        /// </summary>
        public void MarkStarLightCarried()
        {
            StarLightCatchTarget = null;
            IsCarryingStarLight = true;
        }

        /// <summary>
        /// 清理当前星光任务。
        /// </summary>
        public void ClearStarLightTask()
        {
            StarLightCatchTarget = null;
            IsCarryingStarLight = false;
        }

        /// <summary>
        /// 生成玩家同高度附近的星光投递点。
        /// </summary>
        public void CreateStarLightReturnPosition()
        {
            Transform player = Camera.main != null ? Camera.main.transform : transform;
            Vector2 offset = Random.insideUnitCircle * starLightReturnRadius;
            Vector3 playerPosition = player.position;

            StarLightReturnPosition = new Vector3(
                playerPosition.x + offset.x,
                -1,
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
                starLightCatchRotateSpeed * Time.deltaTime
            );
        }

        /// <summary>
        /// 判断翼龙是否已经进入目标点指定半径。
        /// </summary>
        public bool IsNearPosition(Vector3 position, float distance)
        {
            return (transform.position - position).sqrMagnitude <= distance * distance;
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
