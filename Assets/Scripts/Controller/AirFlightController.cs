using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public enum AirFlightState
{
    Idle,
    Flying,
    Waiting,
    Paused
}

[DisallowMultipleComponent]
public class AirFlightController : MonoBehaviour
{
    [System.Serializable]
    public struct WaypointConfig
    {
        [Tooltip("航点的位置")]
        public Transform point;
        
        [Tooltip("飞往该航点时的特定动画 Bool 参数名，留空则使用全局配置")]
        public string overrideFlyingBoolParam;
        
        [Tooltip("在该航点等待时的特定动画 Bool 参数名，留空则使用全局配置")]
        public string overrideWaitingBoolParam;
        
        [Tooltip("飞往该点的特定速度（大于 0 时生效，否则使用全局 speed）")]
        public float overrideSpeed;
        
        [Tooltip("在该点的等待时间（大于或等于 0 时生效，否则使用全局 waitDuration）")]
        public float overrideWaitDuration;
    }

    [Header("Waypoints Configuration")]
    [Tooltip("航点配置列表")]
    [SerializeField] private List<WaypointConfig> waypoints = new List<WaypointConfig>();
    
    [Tooltip("是否循环飞行航点列表")]
    [SerializeField] private bool loop = true;
    
    [Tooltip("是否在 Start 时自动开启多航点飞行")]
    [SerializeField] private bool moveOnStart = false;

    [Header("Global Movement Settings")]
    [Tooltip("默认飞行速度（米/秒）")]
    [SerializeField] private float speed = 5f;
    
    [Tooltip("默认等待时间（秒）")]
    [SerializeField] private float waitDuration = 0f;
    
    [Tooltip("飞行插值曲线类型")]
    [SerializeField] private Ease easeType = Ease.Linear;

    [Header("Rotation Settings")]
    [Tooltip("飞行时是否自动朝向移动方向")]
    [SerializeField] private bool rotateTowardsDirection = true;
    
    [Tooltip("是否只在水平面上旋转（锁定仰角和横滚角，只进行左右转向）")]
    [SerializeField] private bool lockPitchAndRoll = false;

    [Tooltip("朝向移动方向的旋转插值速度（仅在分段和单次飞行的自定义平滑旋转中生效）")]
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Animation Settings")]
    [Tooltip("动画控制器引用")]
    [SerializeField] private Animator animator;
    
    [Tooltip("默认飞行状态的 Animator 参数名")]
    [SerializeField] private string flyingBoolParam = "IsFlying";
    
    [Tooltip("默认等待状态的 Animator 参数名")]
    [SerializeField] private string waitingBoolParam = "IsWaiting";
    
    [Tooltip("空闲状态 of the Animator 参数名")]
    [SerializeField] private string idleBoolParam = "IsIdle";

    // 状态与流程控制
    private AirFlightState _currentState = AirFlightState.Idle;
    private AirFlightState _prevStateBeforePause = AirFlightState.Idle;
    private bool _isPaused = false;
    
    private Coroutine _flightCoroutine;
    private Coroutine _waitCoroutine;
    private Tween _currentTween;

    // 当前段有效的动画参数缓存，用于 Pause / Resume 恢复
    private string _activeFlyingBoolParam;
    private string _activeWaitingBoolParam;
    
    // 当前处于开启（true）状态的 Animator 变量名缓存
    private string _currentActiveAnimParam;
    // 记录所有涉及的动画参数，用于 Stop 时一键清理兜底
    private HashSet<string> _allAnimBools = new HashSet<string>();

    // 旋转朝向计算辅助
    private Vector3 _lastPosition;
    private bool _useInternalRotation = false; // 在 DOPath 模式下使用 DOTween 的 LookAt 系统，无需 LateUpdate 干扰

    #region Public Properties

    /// <summary>
    /// 获取当前飞行状态
    /// </summary>
    public AirFlightState CurrentState => _currentState;

    /// <summary>
    /// 获取航点列表配置
    /// </summary>
    public List<WaypointConfig> Waypoints => waypoints;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        _lastPosition = transform.position;

        if (moveOnStart && waypoints != null && waypoints.Count > 0)
        {
            StartWaypointMovement();
        }
        else
        {
            _currentState = AirFlightState.Idle;
            UpdateAnimator(AirFlightState.Idle);
        }
    }

    private void LateUpdate()
    {
        // 仅在非 DOPath 模式（如分段或单点飞行）下使用自定义的 LateUpdate 平滑旋转，避免与 DOTween 的 LookAt 冲突
        if (rotateTowardsDirection && _currentState == AirFlightState.Flying && !_isPaused && !_useInternalRotation)
        {
            Vector3 currentPos = transform.position;
            Vector3 dir = currentPos - _lastPosition;

            if (lockPitchAndRoll)
            {
                dir.y = 0f;
            }

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }
        _lastPosition = transform.position;
    }

    private void OnDestroy()
    {
        if (_currentTween != null)
        {
            _currentTween.Kill();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// 立即向目标点开始一次平滑飞行（忽略避障，单次飞行不可被打断，除非调用 Stop/Pause）
    /// </summary>
    public void MoveTo(Vector3 targetPosition)
    {
        if (_currentState == AirFlightState.Flying || _currentState == AirFlightState.Waiting)
        {
            Debug.LogWarning($"[AirFlightController] Cannot MoveTo when state is {_currentState}");
            return;
        }

        StopAllMovementCoroutines();
        _lastPosition = transform.position; // 重置旋转参考点
        _useInternalRotation = false;       // MoveTo 使用 LateUpdate 平滑自转
        _flightCoroutine = StartCoroutine(FlyToCoroutine(targetPosition));
    }

    /// <summary>
    /// 开启多航点配置列表移动流程
    /// </summary>
    public void StartWaypointMovement()
    {
        if (_currentState == AirFlightState.Flying || _currentState == AirFlightState.Waiting)
        {
            Debug.LogWarning($"[AirFlightController] Cannot StartWaypointMovement when state is {_currentState}");
            return;
        }

        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogWarning("[AirFlightController] Waypoint configuration list is empty.");
            return;
        }

        StopAllMovementCoroutines();
        _lastPosition = transform.position; // 重置旋转参考点
        _flightCoroutine = StartCoroutine(FollowWaypointsCoroutine());
    }

    /// <summary>
    /// 暂停当前飞行，并将状态记录为 Paused
    /// </summary>
    public void Pause()
    {
        if (_currentState == AirFlightState.Paused || _currentState == AirFlightState.Idle)
            return;

        _isPaused = true;
        _prevStateBeforePause = _currentState;
        _currentState = AirFlightState.Paused;

        if (_currentTween != null && _currentTween.IsActive())
        {
            _currentTween.Pause();
        }

        UpdateAnimator(AirFlightState.Paused);
    }

    /// <summary>
    /// 从暂停状态恢复飞行与计时
    /// </summary>
    public void Resume()
    {
        if (_currentState != AirFlightState.Paused)
            return;

        _isPaused = false;
        _currentState = _prevStateBeforePause;
        _lastPosition = transform.position; // 恢复时同步位置，防止瞬间大幅度转向旋转突变

        if (_currentTween != null && _currentTween.IsActive())
        {
            _currentTween.Play();
        }

        ReapplyCurrentStateAnimation();
    }

    /// <summary>
    /// 停止飞行，清除当前航点追踪，将状态完全置为 Idle
    /// </summary>
    public void Stop()
    {
        StopAllMovementCoroutines();

        if (_currentTween != null)
        {
            _currentTween.Kill();
            _currentTween = null;
        }

        _isPaused = false;
        _activeFlyingBoolParam = null;
        _activeWaitingBoolParam = null;
        _currentState = AirFlightState.Idle;
        _useInternalRotation = false;

        ResetAllAnimatorBools();
        if (animator != null && !string.IsNullOrEmpty(idleBoolParam))
        {
            animator.SetBool(idleBoolParam, true);
            _currentActiveAnimParam = idleBoolParam;
        }
    }

    #endregion

    #region Movement Coroutines

    private void StopAllMovementCoroutines()
    {
        if (_flightCoroutine != null)
            StopCoroutine(_flightCoroutine);
        if (_waitCoroutine != null)
            StopCoroutine(_waitCoroutine);
        _flightCoroutine = null;
        _waitCoroutine = null;
    }

    private IEnumerator FlyToCoroutine(Vector3 targetPosition)
    {
        _currentState = AirFlightState.Flying;
        
        _activeFlyingBoolParam = flyingBoolParam;
        _activeWaitingBoolParam = null;
        
        if (!string.IsNullOrEmpty(_activeFlyingBoolParam))
        {
            SetAnimatorBool(_activeFlyingBoolParam, true);
        }

        Vector3 startPos = transform.position;
        float dist = Vector3.Distance(startPos, targetPosition);
        float duration = speed > 0f ? dist / speed : 0.1f;

        _currentTween = transform.DOMove(targetPosition, duration).SetEase(easeType);

        yield return _currentTween.WaitForCompletion();

        _currentState = AirFlightState.Idle;
        UpdateAnimator(AirFlightState.Idle);
        _flightCoroutine = null;
    }

    private IEnumerator FollowWaypointsCoroutine()
    {
        if (ShouldUseDOPath())
        {
            yield return StartCoroutine(FollowPathSmoothCoroutine());
        }
        else
        {
            yield return StartCoroutine(FollowPathSegmentedCoroutine());
        }
    }

    /// <summary>
    /// 连续平滑路径飞行（无停留且无航段特定动画时触发，连成平滑 DOPath）
    /// </summary>
    private IEnumerator FollowPathSmoothCoroutine()
    {
        _currentState = AirFlightState.Flying;
        
        _activeFlyingBoolParam = flyingBoolParam;
        _activeWaitingBoolParam = null;

        if (!string.IsNullOrEmpty(_activeFlyingBoolParam))
        {
            SetAnimatorBool(_activeFlyingBoolParam, true);
        }

        // 不包含初始位置，仅抓取设定的航点序列，使一圈循环结束后，不再强行折返初始位置
        Vector3[] pathPoints = GetPathPoints(false);
        
        // 总长度 = 航点闭环长度 + 起始点到第一个航点的过渡长度
        float pathLength = CalculatePathLength(pathPoints, loop);
        float distToStart = Vector3.Distance(transform.position, pathPoints[0]);
        float totalLength = pathLength + distToStart;
        float pathDuration = speed > 0f ? totalLength / speed : 1f;

        // 设置朝向旋转约束
        AxisConstraint lockRot = lockPitchAndRoll 
            ? (AxisConstraint.X | AxisConstraint.Z) 
            : AxisConstraint.None;

        // 调用 DOTween 样条曲线路径，传入 Color.clear 关闭默认白线的绘制
        var pathTween = transform.DOPath(pathPoints, pathDuration, PathType.CatmullRom, PathMode.Full3D, 10, Color.clear)
                                 .SetOptions(loop, AxisConstraint.None, lockRot)
                                 .SetEase(easeType);

        // 如果启用朝向旋转，使用 DOTween 原生的 LookAt 以获得最平滑精确的样条切线方向旋转
        if (rotateTowardsDirection)
        {
            pathTween.SetLookAt(0.01f);
            _useInternalRotation = true;
        }
        else
        {
            _useInternalRotation = false;
        }

        _currentTween = pathTween;

        if (loop)
        {
            _currentTween.SetLoops(-1, LoopType.Restart);
        }

        yield return _currentTween.WaitForCompletion();

        _currentState = AirFlightState.Idle;
        UpdateAnimator(AirFlightState.Idle);
        _flightCoroutine = null;
    }

    /// <summary>
    /// 分段路径飞行（有等待或航段特定动画时触发，逐点进行 DOMove 并执行等待）
    /// </summary>
    private IEnumerator FollowPathSegmentedCoroutine()
    {
        int index = 0;
        int count = waypoints.Count;
        _useInternalRotation = false; // 分段移动使用自转 LateUpdate

        while (index < count)
        {
            WaypointConfig config = waypoints[index];
            if (config.point == null)
            {
                index++;
                if (loop && index >= count) index = 0;
                continue;
            }

            // 1. 飞向该航点
            _currentState = AirFlightState.Flying;
            _activeFlyingBoolParam = !string.IsNullOrEmpty(config.overrideFlyingBoolParam)
                ? config.overrideFlyingBoolParam
                : flyingBoolParam;
            _activeWaitingBoolParam = null;

            if (!string.IsNullOrEmpty(_activeFlyingBoolParam))
            {
                SetAnimatorBool(_activeFlyingBoolParam, true);
            }

            Vector3 startPos = transform.position;
            Vector3 endPos = config.point.position;
            float currentSpeed = config.overrideSpeed > 0f ? config.overrideSpeed : speed;
            float duration = currentSpeed > 0f ? Vector3.Distance(startPos, endPos) / currentSpeed : 0.1f;

            _currentTween = transform.DOMove(endPos, duration).SetEase(easeType);

            yield return _currentTween.WaitForCompletion();

            // 2. 在航点等待
            float currentWaitDuration = GetWaitDuration(config);
            if (currentWaitDuration > 0f)
            {
                _currentState = AirFlightState.Waiting;
                _activeFlyingBoolParam = null;
                _activeWaitingBoolParam = !string.IsNullOrEmpty(config.overrideWaitingBoolParam)
                    ? config.overrideWaitingBoolParam
                    : waitingBoolParam;

                if (!string.IsNullOrEmpty(_activeWaitingBoolParam))
                {
                    SetAnimatorBool(_activeWaitingBoolParam, true);
                }

                _waitCoroutine = StartCoroutine(WaitForSecondsCustom(currentWaitDuration));
                yield return _waitCoroutine;
                _waitCoroutine = null;
            }

            index++;
            if (loop && index >= count)
            {
                index = 0;
            }
        }

        _currentState = AirFlightState.Idle;
        UpdateAnimator(AirFlightState.Idle);
        _flightCoroutine = null;
    }

    /// <summary>
    /// 支持暂停的自定义等待协程
    /// </summary>
    private IEnumerator WaitForSecondsCustom(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!_isPaused)
            {
                elapsed += Time.deltaTime;
            }
            yield return null;
        }
    }

    #endregion

    #region Helper Methods

    private float GetWaitDuration(WaypointConfig wp)
    {
        return wp.overrideWaitDuration >= 0f ? wp.overrideWaitDuration : waitDuration;
    }

    /// <summary>
    /// 判断是否应该使用平滑的 DOPath 连续曲线飞行
    /// </summary>
    private bool ShouldUseDOPath()
    {
        if (waypoints == null || waypoints.Count < 2) return false;

        foreach (var wp in waypoints)
        {
            if (GetWaitDuration(wp) > 0f) return false;
            if (!string.IsNullOrEmpty(wp.overrideFlyingBoolParam)) return false;
        }

        return true;
    }

    private Vector3[] GetPathPoints(bool includeCurrentPosition)
    {
        int listCount = waypoints.Count;
        int pointCount = includeCurrentPosition ? listCount + 1 : listCount;
        Vector3[] points = new Vector3[pointCount];

        int idx = 0;
        if (includeCurrentPosition)
        {
            points[0] = transform.position;
            idx = 1;
        }

        for (int i = 0; i < listCount; i++)
        {
            points[idx++] = waypoints[i].point != null ? waypoints[i].point.position : transform.position;
        }

        return points;
    }

    private float CalculatePathLength(Vector3[] points, bool isClosed)
    {
        if (points == null || points.Length < 2) return 0f;
        float len = 0f;
        for (int i = 0; i < points.Length - 1; i++)
        {
            len += Vector3.Distance(points[i], points[i + 1]);
        }
        if (isClosed)
        {
            len += Vector3.Distance(points[points.Length - 1], points[0]);
        }
        return len;
    }

    #endregion

    #region Animation Helpers

    private void SetAnimatorBool(string paramName, bool value)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return;

        if (value)
        {
            if (!string.IsNullOrEmpty(_currentActiveAnimParam) && _currentActiveAnimParam != paramName)
            {
                animator.SetBool(_currentActiveAnimParam, false);
            }
            animator.SetBool(paramName, true);
            _currentActiveAnimParam = paramName;
            _allAnimBools.Add(paramName);
        }
        else
        {
            animator.SetBool(paramName, false);
            if (_currentActiveAnimParam == paramName)
            {
                _currentActiveAnimParam = null;
            }
        }
    }

    private void ResetAllAnimatorBools()
    {
        if (animator == null) return;
        foreach (var param in _allAnimBools)
        {
            animator.SetBool(param, false);
        }
        _currentActiveAnimParam = null;
    }

    private void UpdateAnimator(AirFlightState state)
    {
        if (animator == null) return;

        switch (state)
        {
            case AirFlightState.Idle:
                if (!string.IsNullOrEmpty(idleBoolParam))
                    SetAnimatorBool(idleBoolParam, true);
                break;
            case AirFlightState.Flying:
                if (!string.IsNullOrEmpty(flyingBoolParam))
                    SetAnimatorBool(flyingBoolParam, true);
                break;
            case AirFlightState.Waiting:
                if (!string.IsNullOrEmpty(waitingBoolParam))
                    SetAnimatorBool(waitingBoolParam, true);
                break;
            case AirFlightState.Paused:
                if (!string.IsNullOrEmpty(idleBoolParam))
                    SetAnimatorBool(idleBoolParam, true);
                break;
        }
    }

    private void ReapplyCurrentStateAnimation()
    {
        if (_currentState == AirFlightState.Flying && !string.IsNullOrEmpty(_activeFlyingBoolParam))
        {
            SetAnimatorBool(_activeFlyingBoolParam, true);
        }
        else if (_currentState == AirFlightState.Waiting && !string.IsNullOrEmpty(_activeWaitingBoolParam))
        {
            SetAnimatorBool(_activeWaitingBoolParam, true);
        }
        else
        {
            UpdateAnimator(_currentState);
        }
    }

    #endregion

    #region Editor Visualization (Gizmos)

    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        List<Vector3> validPoints = new List<Vector3>();
        foreach (var wp in waypoints)
        {
            if (wp.point != null)
            {
                validPoints.Add(wp.point.position);
            }
        }

        if (validPoints.Count == 0) return;

        for (int i = 0; i < waypoints.Count; i++)
        {
            var wp = waypoints[i];
            if (wp.point == null) continue;

            bool hasOverride = !string.IsNullOrEmpty(wp.overrideFlyingBoolParam) || 
                               !string.IsNullOrEmpty(wp.overrideWaitingBoolParam) || 
                               wp.overrideSpeed > 0f || 
                               wp.overrideWaitDuration >= 0f;

            Gizmos.color = hasOverride ? new Color(1f, 0.5f, 0f, 0.8f) : new Color(0.2f, 0.8f, 1f, 0.8f);
            Gizmos.DrawSphere(wp.point.position, 0.3f);
        }

        Gizmos.color = Color.green;
        if (ShouldUseDOPath())
        {
            DrawSmoothGizmosPath(validPoints, loop);
        }
        else
        {
            for (int i = 0; i < validPoints.Count - 1; i++)
            {
                Gizmos.DrawLine(validPoints[i], validPoints[i + 1]);
            }
            if (loop && validPoints.Count > 2)
            {
                Gizmos.DrawLine(validPoints[validPoints.Count - 1], validPoints[0]);
            }
        }
    }

    private void DrawSmoothGizmosPath(List<Vector3> points, bool isLoop)
    {
        if (points.Count < 2) return;

        int numPoints = points.Count;
        int segments = isLoop ? numPoints : numPoints - 1;

        for (int i = 0; i < segments; i++)
        {
            Vector3 p0, p1, p2, p3;

            if (isLoop)
            {
                p0 = points[(i - 1 + numPoints) % numPoints];
                p1 = points[i];
                p2 = points[(i + 1) % numPoints];
                p3 = points[(i + 2) % numPoints];
            }
            else
            {
                p1 = points[i];
                p2 = points[i + 1];
                p0 = (i == 0) ? p1 + (p1 - p2) : points[i - 1];
                p3 = (i == numPoints - 2) ? p2 + (p2 - p1) : points[i + 2];
            }

            Vector3 lastPos = p1;
            int resolutions = 20;
            for (int r = 1; r <= resolutions; r++)
            {
                float t = (float)r / resolutions;
                Vector3 pos = GetCatmullRomPosition(t, p0, p1, p2, p3);
                Gizmos.DrawLine(lastPos, pos);
                lastPos = pos;
            }
        }
    }

    private Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    #endregion
}
