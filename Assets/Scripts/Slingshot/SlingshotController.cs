using System;
using System.Collections.Generic;
using UnityEngine;
using Core.Trajectory;
using Entity.DodoBird;
using Manager;
using Core.Event;
using Cysharp.Threading.Tasks;

namespace Slingshot
{
    /// <summary>
    /// 整个小游戏的控制，包括
    /// 1. 所有渡渡鸟的队列管理
    /// 2. 控制绳索显示和渡渡鸟的起飞
    /// </summary>
    public class SlingshotController : MonoBehaviour
    {
        #region PrivateComponets
        
        private TrajectoryPredictor _trajectoryPredictor;
        private TrajectoryRenderer _trajectoryRenderer;
        private SlingshotRopeRenderer _ropeRenderer;
        
        #endregion

        #region SerializedFields
        
        // [Header("Birds' Parent Transform")]
        // [SerializeField] private Transform birdParent;
        [Header("槽位（按从前到后顺序排列）")]
        [Tooltip("场景中的站位 Transform，index 0 = 最靠近弹弓的位置。")]
        [SerializeField] private List<Transform> slots = new();
        // 临时拖进去
        [SerializeField] private List<DodoBird> birds = new();
        [Header("初始点")]
        [SerializeField] private Transform startPoint;
        [Tooltip("发射物Transform的偏差")] 
        [SerializeField] private Vector3 offset;
        [SerializeField] private float maxForce = 30f;
        [SerializeField] private float velocityFactor = 2.5f;
        #endregion

        #region PrivateVariables

        /// <summary>当前排队中的鸟，按槽位顺序排列（index 0 = 队首）。</summary>
        private readonly List<DodoBird> _queue = new();
        // 发射点，实际上就是鸟
        private Transform _firePoint;
        private Vector3 _launchVelocity;
        private Vector3 _launchDirection;
        private float _launchForce;
        private bool _isPulling;

        #endregion
        
        #region Lifecycle

        private async void Awake()
        {
            _trajectoryPredictor = GetComponentInChildren<TrajectoryPredictor>();
            _trajectoryRenderer = GetComponentInChildren<TrajectoryRenderer>();
            _ropeRenderer = GetComponentInChildren<SlingshotRopeRenderer>();
            _ropeRenderer.offset = offset;
            
            // Debug.Log("tail slot position " + TailSlotPosition);
            await InitDodoBird();
        }

        private void Update()
        {
            if (_isPulling)
            {
                _launchDirection = startPoint.position - _firePoint.position;
                _launchForce = Mathf.Clamp(_launchDirection.magnitude * 10f, 0, maxForce);
                Vector3 normalizedDir = _launchDirection.normalized;
                if (normalizedDir == Vector3.zero) 
                {
                    normalizedDir = Vector3.forward; // 给个默认前方
                }
                _launchVelocity = normalizedDir * (_launchForce * velocityFactor);
                _trajectoryPredictor.UpdatePreview(_firePoint.position + offset, _launchVelocity);
                _trajectoryRenderer.SetForceRatio(_launchForce / maxForce);
            }
        }

        private void OnEnable()
        {
            // 注册事件
            GameManager.Event.Register<DodoBird>("DodoBird.OnPulling", OnPulling);
            GameManager.Event.Register<DodoBird>("DodoBird.OnRelease", OnRelease);
            GameManager.Event.Register<DodoBird>("DodoBird.OnEnqueue", OnEnqueue);
        }

        private void OnDisable()
        {
            // 注销事件
            GameManager.Event.Unregister("DodoBird.OnPulling");
            GameManager.Event.Unregister("DodoBird.OnRelease");
            GameManager.Event.Unregister("DodoBird.OnEnqueue");
        }
        
        #endregion
        
        #region EventMethods

        private void OnPulling(DodoBird dodoBird)
        {
            // 通知绳索发射物是谁，开启发射轨迹渲染
            _firePoint = dodoBird.transform;
            _isPulling = true;
            _trajectoryPredictor.ShowPreview();
            _ropeRenderer.SetProjectile(dodoBird.transform);
            _ropeRenderer.BeginPull();
            GetComponent<GameAudioManager>().PlaySound("Pull");
        }

        private void OnRelease(DodoBird dodoBird)
        {
            // 释放并发射该渡渡鸟
            dodoBird.LaunchVelocity = _launchVelocity;
            dodoBird.MoveToPos = slots[^1];
            _isPulling = false;
            _trajectoryPredictor.HidePreview();
            _ropeRenderer.ResetInstant();
            CallNextBird();
            GetComponent<GameAudioManager>().PlaySound("Shoot");
        }

        /// <summary>
        /// 将归队的鸟加入队尾，分配最后一个空槽位。
        /// 由 ReturningState 到达目标后调用。
        /// </summary>
        private void OnEnqueue(DodoBird bird)
        {
            int tailSlotIndex = _queue.Count; // 当前队列长度即下一个可用槽位 index
            if (tailSlotIndex >= slots.Count)
            {
                Debug.LogWarning($"[BirdQueueManager] 槽位已满，无法将 {bird.name} 加入队列。");
                return;
            }
            _queue.Add(bird);
            AssignSlot(bird, tailSlotIndex);
        }
        
        #endregion
        
        #region ToolMethods
        
        /// <summary>
        /// 命令鸟更新位置
        /// </summary>
        private async void CallNextBird()
        {
            if (_queue.Count == 0) return;
            _queue.RemoveAt(0);
            for (int i = 0; i < _queue.Count; i++)
            {
                AssignSlot(_queue[i], i);
                _queue[i].IsCalledToNext = true;
                await UniTask.WaitForSeconds(1f);
            }
        }
        
        /// <summary>
        /// 初始化所有的渡渡鸟
        /// </summary>
        private async UniTask InitDodoBird()
        {
            _queue.Clear();
            for (int i = 0; i < slots.Count; i++)
            {
                // GameObject birdGameObject = await GameManager.AssetLoader.LoadPrefab("DodoBird_Lite");
                // DodoBird bird = Instantiate(birdGameObject, slots[i].position, slots[i].rotation, birdParent)
                //     .GetComponent<DodoBird>();
                DodoBird bird = birds[i];
                bird.LoadedPos = startPoint;
                _queue.Add(bird);
                await UniTask.Yield();
                AssignSlot(bird, i);
            }
            await UniTask.Yield();
        }
 
        /// <summary>
        /// 更新鸟的MoveToPos并更新IsFirstInQueue
        /// </summary>
        private void AssignSlot(DodoBird bird, int slotIndex)
        {
            bird.MoveToPos = slots[slotIndex];
            bird.IsFirstInQueue = slotIndex == 0;
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) continue;
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(slots[i].position, 0.15f);
                UnityEditor.Handles.Label(slots[i].position + Vector3.up * 0.2f, $"Slot {i}");
            }
        }
#endif
        
        #endregion
    }
}