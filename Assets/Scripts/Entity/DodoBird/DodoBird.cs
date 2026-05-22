using System;
using Core.Fsm;
using Entity.DodoBird.State;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit;

namespace Entity.DodoBird
{
    /// <summary>
    /// 渡渡鸟宿主组件（MonoBehaviour 层）。
    ///
    /// 职责：
    ///   1. 持有并驱动 FSM
    ///   2. 暴露组件引用与数据供各状态访问
    ///   3. 将 Unity 回调（碰撞、XR 事件）转发给 FSM
    ///
    /// 各状态的具体逻辑封装在 States/ 目录下，DodoBird 本身不包含游戏逻辑。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(Rigidbody))]
    // [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class DodoBird : MonoBehaviour, IAnimator, IAudioSource, ICurStateType<DodoBirdStateType>
    {
        # region Components
        // ─── 组件引用（各状态通过属性访问）─────────────────────────────────
 
        public Rigidbody         Rb          { get; private set; }
        public NavMeshAgent      NavAgent    { get; private set; }
        public Animator          ani        { get; private set; }
        public XRGrabInteractable GrabInteractable { get; private set; }
        public Collider         Collider     { get; private set; }
        public AudioSource       aus          { get; private set; }
        
        #endregion
        
        #region StateMachineVariables

        // bool状态变量
        public bool IsFirstInQueue { get; set; }
        public bool IsCalledToNext { get; set; }
        public bool IsBeGrabbed { get; set; }
        public bool IsBeReleased { get; set; }
        public bool IsLanded { get; set; }
        // 变量
        public float DisToTeleSq => disToTele * disToTele;
        public float OffsetDistance => offsetDistance;
        public Vector3 LaunchVelocity { get; set; }
        // 引用
        public Transform LoadedPos { get; set; }
        public Transform MoveToPos { get; set; }

        #endregion

        #region SerializeFieldVariables
        
        [Header("距离吸附点")]
        [SerializeField] private float disToTele = 3f;
        [Header("前置点Offset")]
        [SerializeField] private float offsetDistance = 2f;
        [Header("粒子效果")] 
        [SerializeField] private ParticleSystem shockPS;
        [SerializeField] private ParticleSystem smogPS;
        [SerializeField] private ParticleSystem cryPS;

        #endregion

        #region PrivateVariables
        
        private static int _landLayer;
        private StateMachine<DodoBirdStateType> _fsm;
        public DodoBirdStateType CurrentStateType => _fsm != null ? _fsm.CurrentKey : DodoBirdStateType.Idle;
        Enum ICurStateType.CurrentStateTypeEnum => CurrentStateType;

        #endregion

        #region XRGrabStateMethod

        private void RegisterGrab(SelectEnterEventArgs args)
        {
            IsBeGrabbed = true;
        }

        private void RegisterRelease(SelectExitEventArgs args)
        {
            IsBeReleased = true;
        }

        #endregion
 
        #region Lifecycle
        // ─── 生命周期 ────────────────────────────────────────────────────────
        private void Awake()
        {
            FetchComponents();
            BuildFsm();
            _landLayer = LayerMask.NameToLayer("Land");
        }
 
        private void Start()
        {
            GrabInteractable.throwOnDetach = false;
            GrabInteractable.selectEntered.AddListener(RegisterGrab);
            GrabInteractable.selectExited.AddListener(RegisterRelease);
            _fsm.Initialize(DodoBirdStateType.Idle);
        }

        private void Update() => _fsm.OnUpdate();

        #endregion

        #region PrivateMethods
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.layer == _landLayer)
            {
                IsLanded = true;
            }
        }
 
        private void FetchComponents()
        {
            Rb               = GetComponent<Rigidbody>();
            NavAgent         = GetComponent<NavMeshAgent>();
            ani             = GetComponent<Animator>();
            GrabInteractable = GetComponent<XRGrabInteractable>();
            Collider         = GetComponent<Collider>();
            aus               = GetComponent<AudioSource>();
        }
 
        private void BuildFsm()
        {
            _fsm = new StateMachine<DodoBirdStateType>();
            _fsm.AddState(DodoBirdStateType.Idle,       new IdleState(this, _fsm, "Idle"));
            _fsm.AddState(DodoBirdStateType.Move,       new MoveState(this, _fsm, "Move"));
            _fsm.AddState(DodoBirdStateType.Wait,       new WaitState(this, _fsm, "Idle"));
            _fsm.AddState(DodoBirdStateType.Grabbed,    new GrabbedState(this, _fsm, null));
            _fsm.AddState(DodoBirdStateType.Loaded,     new LoadedState(this, _fsm, null));
            _fsm.AddState(DodoBirdStateType.Aim,        new AimState(this, _fsm, null));
            _fsm.AddState(DodoBirdStateType.Shot,       new ShotState(this, _fsm, null));
            _fsm.AddState(DodoBirdStateType.Return,     new ReturnState(this, _fsm, "Move"));
 
            // _fsm.OnStateChanged += (from, to) =>
            //     Debug.Log($"[DodoBird:{name}] {from} → {to}");
        }
        #endregion

        #region PublicMethods

        public void PlayParticle(DodoBirdParticleType type)
        {
            ParticleSystem ps = type switch
            {
                DodoBirdParticleType.Shock => shockPS,
                DodoBirdParticleType.Smog  => smogPS,
                DodoBirdParticleType.Cry  => cryPS,
                _ => null
            };
            ps?.Play();
        }

        #endregion
    }

    public enum DodoBirdParticleType
    {
        Shock,
        Smog,
        Cry,
    }
}