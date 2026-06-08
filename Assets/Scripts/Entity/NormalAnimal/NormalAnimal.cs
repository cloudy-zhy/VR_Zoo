using System;
using Core.Fsm;
using Core.Utils;
using Entity.NormalAnimal.State;
using UnityEngine;
using UnityEngine.AI;

namespace Entity.NormalAnimal
{
    /// <summary>
    /// 一般游走可被拍拍的动物
    /// 当处于拍拍状态不会执行其他行为，直到不拍为止
    /// 当移动时被拍拍（如果追的上的话），会停下动作直到被拍完
    /// 将被作为预制件，放到动物下，其父物体需有nav、ani，子物体保证拥有aus
    /// </summary>
    public class NormalAnimal : MonoBehaviour, IAnimator, IAudioSource, ICurStateType<NormalAnimalStateType>
    {
        #region Components
        
        public Animator ani { get; private set; }
        public AudioSource aus { get; private set; }
        public NavMeshAgent nav { get; private set; }
        
        #endregion
        
        #region PrivateVariables
        
        private StateMachine<NormalAnimalStateType> m_fsm;
        public NormalAnimalStateType CurrentStateType => m_fsm?.CurrentKey ?? NormalAnimalStateType.Idle;
        Enum ICurStateType.CurrentStateTypeEnum => CurrentStateType;
        private bool m_isPetting;

        #endregion
        
        #region StateMachineVariables

        // 摸摸状态会调用isStopped，不准乱动
        public bool IsPetting
        {
            get => m_isPetting;
            set
            {
                m_isPetting = value;
                nav.isStopped = value;
            }
        }
        public Vector3 Destination { get; set; }
        
        #endregion
        
        #region PublicVariables

        [Header("闲置状态设定")]
        public float minIdleTime = 2f;
        public float maxIdleTime = 5f;
        [Header("移动状态设定")] 
        public BoxCollider moveableArea;
        public float maxDistance = 10f;

        #endregion
        
        #region LifeCycle

        private void Awake()
        {
            m_fsm = new StateMachine<NormalAnimalStateType>();
            m_fsm.AddState(NormalAnimalStateType.Idle, new IdleState(this, m_fsm, "Idle"));
            m_fsm.AddState(NormalAnimalStateType.Move, new MoveState(this, m_fsm, "Move"));
            ani = GetComponentInParent<Animator>();
            aus = GetComponentInChildren<AudioSource>();
            nav = GetComponentInParent<NavMeshAgent>();
            
            if (moveableArea.IsNull())
                Debug.LogError("未设置可移动区域！");
        }

        private void Start()
        {
            m_fsm.Initialize(NormalAnimalStateType.Idle);
        }
        
        private void Update() => m_fsm.OnUpdate();

        #endregion
    }
}