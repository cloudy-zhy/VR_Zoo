using System;
using System.Collections.Generic;
using UnityEngine;
using Core.Trajectory;
using Entity.DodoBird;
using Manager;
using Core.Event;
using Cysharp.Threading.Tasks;
using UnityEngine.XR.Interaction.Toolkit;

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

        [Header("拉弓手感")]
        [Tooltip("最大拉动物理距离（单位：米）。超过此距离后鸟将无法继续被拉远，力度达到最大。")]
        [SerializeField] private float maxPullDistance = 0.5f;

        [Header("瞄准手感")]
        [Tooltip("横向瞄准灵敏度。数值越小，手横向移动造成的落点偏移越小。")]
        [Range(0.05f, 1f)]
        [SerializeField] private float horizontalAimSensitivity = 0.5f;

        [Tooltip("纵向瞄准灵敏度。数值越小，手上下移动造成的落点偏移越小。")]
        [Range(0.05f, 1f)]
        [SerializeField] private float verticalAimSensitivity = 0.5f;

        [Header("锁定瞄准")]
        [Tooltip("锁定轨迹线的固定颜色，不受当前拉力影响。")]
        [SerializeField] private Color lockedTrajectoryColor = new(0.1f, 0.85f, 1f, 0.95f);
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
        private LineRenderer _lockedTrajectoryLine;
        private Material _lockedTrajectoryMaterialInstance;
        private SlingshotFruit _lockedTarget;
        private TrajectoryResult _lockedTrajectoryResult;
        private Vector3 _lockedStartPosition;
        private Vector3 _lockedBirdPosition;
        private Vector3 _lockedLaunchVelocity;
        private Vector3 _lockedHitPoint;
        private Vector3 _lockedHitNormal = Vector3.up;

        private bool HasLockedTarget => _lockedTarget != null && _lockedTrajectoryResult != null;
        public DodoBird CurrentBird => _queue.Count > 0 ? _queue[0] : null;

        #endregion

        #region Lifecycle

        private async void Awake()
        {
            _trajectoryPredictor = GetComponentInChildren<TrajectoryPredictor>();
            _trajectoryRenderer = GetComponentInChildren<TrajectoryRenderer>();
            _ropeRenderer = GetComponentInChildren<SlingshotRopeRenderer>();
            _ropeRenderer.offset = offset;
            CreateLockedTrajectoryLine();
            
            // Debug.Log("tail slot position " + TailSlotPosition);
            await InitDodoBird();
        }

        private void Update()
        {
            if (_isPulling)
            {
                // 直接使用渡渡鸟当前的物理 Transform 位置进行计算
                Vector3 rawLaunchDirection = startPoint.position - _firePoint.position;
                _launchDirection = GetAimAdjustedLaunchDirection(rawLaunchDirection);
                
                // 根据最大物理拉伸距离，计算归一化的拉力（超出 maxPullDistance 后拉力不再增加，且拉满 maxPullDistance 时力度为 maxForce）
                float currentPullDist = rawLaunchDirection.magnitude;
                float pullRatio = Mathf.Clamp01(currentPullDist / maxPullDistance);
                _launchForce = pullRatio * maxForce;

                Vector3 normalizedDir = _launchDirection.normalized;
                if (normalizedDir == Vector3.zero) 
                {
                    normalizedDir = Vector3.forward; // 给个默认前方
                }
                _launchVelocity = normalizedDir * (_launchForce * velocityFactor);

                // 基于当前渡渡鸟实际位置作为起点更新预览
                Vector3 previewStartPosition = _firePoint.position + offset;
                
                TrajectoryResult previewResult = _trajectoryPredictor.UpdatePreview(previewStartPosition, _launchVelocity);
                _trajectoryRenderer.SetForceRatio(_launchForce / maxForce);
                TryUpdateLockedTarget(previewResult, previewStartPosition, _launchVelocity);
            }
        }

        private void OnEnable()
        {
            // 注册事件
            GameManager.Event.Register<DodoBird>("DodoBird.OnPulling", OnPulling);
            GameManager.Event.Register<DodoBird>("DodoBird.OnRelease", OnRelease);
            GameManager.Event.Register<DodoBird>("DodoBird.OnEnqueue", OnEnqueue);
            // 注册剧情事件
            RegisterStoryEvent();
        }

        private void OnDisable()
        {
            // 注销事件
            GameManager.Event.Unregister<DodoBird>("DodoBird.OnPulling", OnPulling);
            GameManager.Event.Unregister<DodoBird>("DodoBird.OnRelease", OnRelease);
            GameManager.Event.Unregister<DodoBird>("DodoBird.OnEnqueue", OnEnqueue);
            ClearLockedTarget();
            // 注销剧情事件
            UnregisterStoryEvent();
        }

        private void OnDestroy()
        {
            if (_lockedTrajectoryMaterialInstance != null)
                Destroy(_lockedTrajectoryMaterialInstance);
        }
        
        #endregion
        
        #region EventMethods

        private void OnPulling(EventContext<DodoBird> context)
        {
            DodoBird dodoBird = context.Payload;

            // 通知绳索发射物是谁，开启发射轨迹渲染
            _firePoint = dodoBird.transform;
            _isPulling = true;
            ClearLockedTarget();
            _trajectoryPredictor.ShowPreview();
            _ropeRenderer.SetProjectile(dodoBird.transform);
            _ropeRenderer.BeginPull();
            //GetComponent<GameAudioManager>().PlaySound("Pull");
            AudioManagerGlobal.Instance.Play("Pull");
            Invoke("PlayPowerLoop",1f);
        }

        private void PlayPowerLoop()
        {
            AudioManagerGlobal.Instance.Play("powerloop");
        }
        private void StopPowerLoop()
        {
            CancelInvoke("PlayPowerLoop");
            AudioManagerGlobal.Instance.Stop("powerloop");
        }
        private void OnRelease(EventContext<DodoBird> context)
        {
            DodoBird dodoBird = context.Payload;

            // 释放并发射该渡渡鸟
            if (HasLockedTarget)
                ApplyLockedShot(dodoBird);
            else
                dodoBird.LaunchVelocity = _launchVelocity;

            dodoBird.MoveToPos = slots[^1];
            _isPulling = false;
            _trajectoryPredictor.HidePreview();
            ClearLockedTarget();
            _ropeRenderer.ResetInstant();
            CallNextBird();
            StopPowerLoop();
            AudioManagerGlobal.Instance.Play("Shoot");
            //GetComponent<GameAudioManager>().PlaySound("Shoot");
            //Invoke("StopPowerLoop",1f);
        }

        /// <summary>
        /// 将归队的鸟加入队尾，分配最后一个空槽位。
        /// 由 ReturningState 到达目标后调用。
        /// </summary>
        private void OnEnqueue(EventContext<DodoBird> context)
        {
            DodoBird bird = context.Payload;

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

        /// <summary>
        /// 运行时复制当前轨迹线配置，生成一条独立的锁定轨迹线。
        /// </summary>
        private void CreateLockedTrajectoryLine()
        {
            if (_trajectoryRenderer == null) return;

            LineRenderer source = _trajectoryRenderer.GetComponent<LineRenderer>();
            if (source == null) return;

            GameObject lineObject = new("LockedTrajectory_Line");
            lineObject.transform.SetParent(_trajectoryRenderer.transform, false);

            _lockedTrajectoryLine = lineObject.AddComponent<LineRenderer>();
            CopyLineRendererSettings(source, _lockedTrajectoryLine);

            Shader fallbackShader = Shader.Find("Sprites/Default");
            _lockedTrajectoryMaterialInstance = source.sharedMaterial != null
                ? new Material(source.sharedMaterial)
                : new Material(fallbackShader);

            _lockedTrajectoryLine.material = _lockedTrajectoryMaterialInstance;
            _lockedTrajectoryLine.colorGradient = CreateLockedTrajectoryGradient();
            _lockedTrajectoryLine.enabled = false;
            _lockedTrajectoryLine.positionCount = 0;
        }

        private void CopyLineRendererSettings(LineRenderer source, LineRenderer target)
        {
            target.useWorldSpace = source.useWorldSpace;
            target.loop = false;
            target.widthCurve = source.widthCurve;
            target.widthMultiplier = source.widthMultiplier;
            target.numCornerVertices = source.numCornerVertices;
            target.numCapVertices = source.numCapVertices;
            target.alignment = source.alignment;
            target.textureMode = source.textureMode;
            target.shadowCastingMode = source.shadowCastingMode;
            target.receiveShadows = source.receiveShadows;
            target.generateLightingData = source.generateLightingData;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder;
        }

        private Gradient CreateLockedTrajectoryGradient()
        {
            Gradient gradient = new();
            Color endColor = new(
                lockedTrajectoryColor.r,
                lockedTrajectoryColor.g,
                lockedTrajectoryColor.b,
                0f);

            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(lockedTrajectoryColor, 0f),
                    new GradientColorKey(endColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(lockedTrajectoryColor.a, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        /// <summary>
        /// 当前预览轨迹命中新的果子时，替换锁定结果并广播事件。
        /// </summary>
        private void TryUpdateLockedTarget(
            TrajectoryResult previewResult,
            Vector3 previewStartPosition,
            Vector3 previewLaunchVelocity)
        {
            if (previewResult == null ||
                !previewResult.HasLanding ||
                previewResult.HitCollider == null ||
                !previewResult.LandingPoint.HasValue)
            {
                return;
            }
            
            SlingshotFruit fruit = previewResult.HitCollider.GetComponentInParent<SlingshotFruit>();
            if (fruit == null) return;

            bool isNewTarget = fruit != _lockedTarget;

            _lockedTarget = fruit;
            _lockedTrajectoryResult = previewResult;
            _lockedStartPosition = previewStartPosition;
            _lockedBirdPosition = _firePoint != null ? _firePoint.position : previewStartPosition - offset;
            _lockedLaunchVelocity = previewLaunchVelocity;
            _lockedHitPoint = previewResult.LandingPoint.Value;
            _lockedHitNormal = previewResult.LandingNormal;

            UpdateLockedTrajectoryLine(previewResult);
            if (isNewTarget)
                this.Broadcast("DodoBird.FruitLocked");
        }

        /// <summary>
        /// 降低拉弓位移到发射方向的角度敏感度，但保留原始拉弓距离计算力度。
        /// </summary>
        private Vector3 GetAimAdjustedLaunchDirection(Vector3 rawLaunchDirection)
        {
            if (rawLaunchDirection == Vector3.zero) return rawLaunchDirection;

            Transform reference = startPoint != null ? startPoint : transform;
            Vector3 localDirection = reference.InverseTransformDirection(rawLaunchDirection);
            localDirection.x *= horizontalAimSensitivity;
            localDirection.y *= verticalAimSensitivity;

            return reference.TransformDirection(localDirection);
        }

        private void UpdateLockedTrajectoryLine(TrajectoryResult result)
        {
            if (_lockedTrajectoryLine == null) return;

            if (result == null || result.Points.Count < 2)
            {
                _lockedTrajectoryLine.positionCount = 0;
                _lockedTrajectoryLine.enabled = false;
                return;
            }

            int count = result.Points.Count;
            Vector3[] positions = new Vector3[count];
            for (int i = 0; i < count; i++)
                positions[i] = result.Points[i];

            _lockedTrajectoryLine.positionCount = count;
            _lockedTrajectoryLine.SetPositions(positions);
            _lockedTrajectoryLine.enabled = true;
        }

        private void ApplyLockedShot(DodoBird dodoBird)
        {
            Vector3 shotDirection = _lockedLaunchVelocity.sqrMagnitude > Mathf.Epsilon
                ? _lockedLaunchVelocity.normalized
                : (_lockedHitPoint - _lockedStartPosition).normalized;

            if (shotDirection == Vector3.zero)
                shotDirection = -_lockedHitNormal;

            if (shotDirection == Vector3.zero)
                shotDirection = Vector3.forward;

            dodoBird.transform.position = _lockedBirdPosition;
            dodoBird.LaunchVelocity = _lockedLaunchVelocity.sqrMagnitude > Mathf.Epsilon
                ? _lockedLaunchVelocity
                : shotDirection * Mathf.Max(1f, _launchForce * velocityFactor);
        }

        private void ClearLockedTarget()
        {
            _lockedTarget = null;
            _lockedTrajectoryResult = null;
            _lockedStartPosition = Vector3.zero;
            _lockedBirdPosition = Vector3.zero;
            _lockedLaunchVelocity = Vector3.zero;
            _lockedHitPoint = Vector3.zero;
            _lockedHitNormal = Vector3.up;

            if (_lockedTrajectoryLine == null) return;

            _lockedTrajectoryLine.positionCount = 0;
            _lockedTrajectoryLine.enabled = false;
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

        #region  ShitMethod / StoryMethod
        // 屎先这么放着先Orz
        private readonly HashSet<string> _finishStoryKeys = new();

        private void RegisterStoryEvent()
        {
            GameManager.Event.Register("DodoBird.Grabbed", OnStoryProceed);
            GameManager.Event.Register("DodoBird.Loaded", OnStoryProceed);
            GameManager.Event.Register("DodoBird.Aimed", OnStoryProceed);
            GameManager.Event.Register("DodoBird.FruitLocked", OnStoryProceed);
            GameManager.Event.Register("DodoBird.FruitHit", OnStoryProceed);
        }
        
        private void UnregisterStoryEvent()
        {
            GameManager.Event.Unregister("DodoBird.Grabbed", OnStoryProceed);
            GameManager.Event.Unregister("DodoBird.Loaded", OnStoryProceed);
            GameManager.Event.Unregister("DodoBird.Aimed", OnStoryProceed);
            GameManager.Event.Unregister("DodoBird.FruitLocked", OnStoryProceed);
            GameManager.Event.Unregister("DodoBird.FruitHit", OnStoryProceed);
        }

        private void OnStoryProceed(EventContext context)
        {
            string key = context.EventName;

            if (CheckKey(key))
            {
                DialogueController.Instance.ShowDialogueWithIndex();
                GameManager.Event.Unregister(key, OnStoryProceed);
            }
        }

        private bool CheckKey(string key)
        {
            // 已经完成，key已存在，add失败，返回false
            return _finishStoryKeys.Add(key);
        }

        #endregion
    }
}
