using System;
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
    public class Pterosaur : MonoBehaviour, IAnimator
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

        
        #endregion
        
        #region PrivateVariables
        
        private StateMachine<PterosaurStateType> _fsm;
        private int _remainReqs;
        public PterosaurStateType CurrentStateType => _fsm != null ? _fsm.CurrentKey : PterosaurStateType.Idle;
        
        #endregion

        #region SerializeFieldVariables
        
        [Header("随机飞行")]
        [Tooltip("随机飞行选取半径")]
        [SerializeField] private float randomRadius = 20f;
        [Tooltip("随机选取最大次数，次数过小可能导致选取失败，不进行随机飞行")]
        [SerializeField] private int randomRetryTimes = 20;

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
