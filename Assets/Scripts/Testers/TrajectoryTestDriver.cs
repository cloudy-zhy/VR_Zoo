using Core.Trajectory;
using UnityEngine;

namespace Testers
{
    public class TrajectoryTestDriver : MonoBehaviour
    {
        [Header("核心系统引用")]
        [Tooltip("拖入挂载了 TrajectoryPredictor 的对象")]
        public TrajectoryPredictor predictor;
        [Tooltip("拖入挂载了 TrajectoryRenderer 的对象")]
        public TrajectoryRenderer rendererObj;
        [Tooltip("拖入玩家当前使用的相机（优先使用这个，而不是自动找 Main Camera）。")]
        public Camera playerCamera;
        [Tooltip("备用发射点；若主摄像机不存在，则使用这里作为起点")]
        public Transform firePoint;

        [Header("发射物配置")]
        [Tooltip("可选：指定要发射的 Prefab。若为空，将在运行时自动创建临时球体。")]
        public GameObject projectilePrefab;
        [Tooltip("发射物生成位置沿瞄准方向的微小偏移，避免与摄像机自身重叠。")]
        [Min(0f)]
        public float spawnForwardOffset = 0.25f;
        [Tooltip("发射物的自动销毁时间，避免测试时场景里堆积太多物体。")]
        [Min(0.5f)]
        public float projectileLifetime = 8f;

        [Header("桌面版 VR / FPS 视角控制")]
        [Tooltip("鼠标水平/垂直看向灵敏度。")]
        [Min(0.01f)]
        public float mouseSensitivity = 2.2f;
        [Tooltip("摄像机上下俯仰的最大角度。")]
        [Range(10f, 89f)]
        public float pitchClamp = 75f;
        [Tooltip("运行时锁定鼠标到屏幕中央，模拟 FPS / VR 视角。")]
        public bool lockCursorOnPlay = true;
        [Tooltip("让发射器 firePoint 绑定到主摄像机前方，始终跟随视角。")]
        public bool bindFirePointToCamera = true;
        [Tooltip("发射器相对摄像机的本地偏移。x=左右，y=上下，z=前后。")]
        public Vector3 firePointCameraOffset = new Vector3(0f, -0.08f, 0.55f);

        [Header("桌面版 VR 投掷测试")]
        [Tooltip("按住 Q 蓄力时的初始力度。")]
        [Min(0.1f)]
        public float minLaunchForce = 6f;
        [Tooltip("按住 Q 蓄力可达到的最大力度。")]
        [Min(0.1f)]
        public float maxForce = 18f;
        [Tooltip("从最小力度蓄到最大力度需要的时间（秒）。")]
        [Min(0.05f)]
        public float chargeDuration = 1.2f;
        [Tooltip("当前摄像机前方多远的位置作为默认瞄准点参考。")]
        public float fallbackAimDistance = 12f;
        [Tooltip("在当前视角前方基础上额外增加的向上抬角，数值越大，抛射越偏上。")]
        [Range(0f, 0.6f)]
        public float aimVerticalBoost = 0.18f;

        [Header("最小果实闭环目标")]
        [Tooltip("可选：拖入现成果实对象。若为空，将在运行时自动创建占位果实。")]
        public GameObject fruitObject;
        [Tooltip("自动创建果实的世界坐标位置。")]
        public Vector3 fruitWorldPosition = new Vector3(0f, 0.7f, 10f);
        [Tooltip("自动创建果实的尺寸。")]
        public Vector3 fruitScale = new Vector3(0.8f, 0.8f, 0.8f);
        [Tooltip("该果实命中时获得的分数。")]
        public int fruitPoints = 10;
        [Tooltip("果实被命中后的最小处理方式。")]
        public FruitHitReaction fruitHitReaction = FruitHitReaction.Drop;
        [Tooltip("若选择隐藏模式，果实多久后恢复显示。设为 0 表示不自动恢复。")]
        [Min(0f)]
        public float fruitRespawnDelay = 1.5f;

        private bool _isAiming;
        private float _chargeTimer;
        private float _currentLaunchForce;
        private Vector3 _currentLaunchDirection = Vector3.forward;
        private float _yaw;
        private float _pitch;
        private int _score;
        private string _statusMessage = "按住 Q 蓄力，松开 Q 发射";
        private float _statusMessageUntil;

        private void Start()
        {
            EnsureRuntimeFruit();
            _currentLaunchForce = minLaunchForce;
            InitializeMouseLook();
            SetStatusMessage("移动鼠标转动视角，按住 Q 蓄力，松开 Q 发射", 2f);
        }

        private void Update()
        {
            if (!HasRequiredReferences()) return;

            UpdateMouseLook();
            UpdateBoundFirePoint();

            if (Input.GetKeyDown(KeyCode.Q))
            {
                BeginAim();
            }

            if (_isAiming && Input.GetKey(KeyCode.Q))
            {
                UpdateAim();
            }

            if (_isAiming && Input.GetKeyUp(KeyCode.Q))
            {
                ReleaseThrow();
            }
        }

        private void OnGUI()
        {
            GUIStyle scoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(20f, 20f, 320f, 40f), $"Score: {_score}", scoreStyle);

            GUIStyle forceStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan }
            };
            GUI.Label(new Rect(20f, 55f, 420f, 35f), $"Force: {_currentLaunchForce:F1}", forceStyle);

            if (Time.time <= _statusMessageUntil)
            {
                GUIStyle messageStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 28,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.yellow }
                };
                GUI.Label(new Rect(Screen.width * 0.5f - 260f, 40f, 520f, 50f), _statusMessage, messageStyle);
            }
        }

        private void InitializeMouseLook()
        {
            Camera cam = GetActiveCamera();
            if (cam == null) return;

            Vector3 euler = cam.transform.eulerAngles;
            _yaw = euler.y;
            _pitch = NormalizeAngle(euler.x);

            if (lockCursorOnPlay)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void UpdateMouseLook()
        {
            Camera cam = GetActiveCamera();
            if (cam == null) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            _yaw += mouseX;
            _pitch -= mouseY;
            _pitch = Mathf.Clamp(_pitch, -pitchClamp, pitchClamp);

            cam.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void UpdateBoundFirePoint()
        {
            Camera activeCamera = GetActiveCamera();
            if (!bindFirePointToCamera || firePoint == null || activeCamera == null)
                return;

            Transform camTransform = activeCamera.transform;
            firePoint.position = camTransform.TransformPoint(firePointCameraOffset);
            firePoint.rotation = camTransform.rotation;
        }

        private float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }

        private void BeginAim()
        {
            _isAiming = true;
            _chargeTimer = 0f;
            _currentLaunchForce = minLaunchForce;
            _currentLaunchDirection = GetAimDirection();
            predictor.ShowPreview();
            SetStatusMessage("蓄力中... 松开 Q 发射", 1f);
            Debug.Log("【测试系统】按下 Q：开始蓄力瞄准...");
        }

        private void UpdateAim()
        {
            _chargeTimer += Time.deltaTime;
            float chargeRatio = Mathf.Clamp01(_chargeTimer / chargeDuration);
            _currentLaunchForce = Mathf.Lerp(minLaunchForce, maxForce, chargeRatio);
            _currentLaunchDirection = GetAimDirection();

            Vector3 startPos = GetThrowOrigin();
            Vector3 initialVelocity = _currentLaunchDirection * _currentLaunchForce;

            predictor.UpdatePreview(startPos, initialVelocity);
            rendererObj.SetForceRatio(Mathf.Clamp01(_currentLaunchForce / maxForce));
        }

        private void ReleaseThrow()
        {
            _isAiming = false;
            predictor.HidePreview();

            Vector3 finalVelocity = _currentLaunchDirection * _currentLaunchForce;
            LaunchProjectile(finalVelocity);
        }

        private void LaunchProjectile(Vector3 finalVelocity)
        {
            Vector3 spawnPosition = GetThrowOrigin() + _currentLaunchDirection * spawnForwardOffset;
            Quaternion spawnRotation = Quaternion.LookRotation(_currentLaunchDirection, Vector3.up);

            GameObject projectile = projectilePrefab != null
                ? Instantiate(projectilePrefab, spawnPosition, spawnRotation)
                : CreateRuntimeProjectile(spawnPosition, spawnRotation);

            if (!projectile.TryGetComponent(out Rigidbody rb))
                rb = projectile.AddComponent<Rigidbody>();

            RuntimeProjectile projectileReporter = projectile.GetComponent<RuntimeProjectile>();
            if (projectileReporter == null)
                projectileReporter = projectile.AddComponent<RuntimeProjectile>();
            projectileReporter.Initialize(this, projectileLifetime);

            rb.velocity = finalVelocity;

            SetStatusMessage("发射！", 0.8f);
            Debug.Log($"【测试系统】松开 Q：发射小球！位置: {spawnPosition}，速度: {finalVelocity}");
        }

        private GameObject CreateRuntimeProjectile(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "Runtime Test Projectile";
            projectile.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            projectile.transform.localScale = Vector3.one * 0.25f;

            Renderer rendererComponent = projectile.GetComponent<Renderer>();
            if (rendererComponent != null)
                rendererComponent.material.color = new Color(0.95f, 0.92f, 0.75f, 1f);

            projectile.AddComponent<Rigidbody>();
            return projectile;
        }

        private void EnsureRuntimeFruit()
        {
            Vector3 desiredFruitPosition = fruitWorldPosition;

            if (fruitObject == null)
            {
                fruitObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fruitObject.name = "Runtime Berry Fruit";
                fruitObject.transform.position = desiredFruitPosition;
                fruitObject.transform.localScale = fruitScale;

                Renderer rendererComponent = fruitObject.GetComponent<Renderer>();
                if (rendererComponent != null)
                    rendererComponent.material.color = new Color(0.9f, 0.2f, 0.35f, 1f);
            }
            else
            {
                fruitObject.transform.position = desiredFruitPosition;
                fruitObject.transform.localScale = fruitScale;
            }

            if (!fruitObject.TryGetComponent(out SimpleFruitTarget fruit))
                fruit = fruitObject.AddComponent<SimpleFruitTarget>();

            fruit.Initialize(this, fruitPoints, fruitHitReaction, fruitRespawnDelay, desiredFruitPosition);
        }

        public void RegisterFruitHit(SimpleFruitTarget fruit, RuntimeProjectile projectile, Collision collision)
        {
            _score += fruit.Points;
            SetStatusMessage($"Hit +{fruit.Points}", 1.2f);
            Debug.Log($"【测试系统】命中果实 {fruit.gameObject.name}，得分 +{fruit.Points}，当前总分：{_score}");

            if (projectile != null)
                projectile.MarkAsResolved();
        }

        public void NotifyProjectileExpired(RuntimeProjectile projectile)
        {
            if (projectile == null || projectile.WasResolved) return;

            SetStatusMessage("Miss!", 1f);
            Debug.Log("【测试系统】本次发射未命中目标。");
        }

        private Vector3 GetThrowOrigin()
        {
            if (bindFirePointToCamera && firePoint != null)
                return firePoint.position;

            Camera activeCamera = GetActiveCamera();
            if (activeCamera != null)
                return activeCamera.transform.position;

            if (firePoint != null)
                return firePoint.position;

            return transform.position;
        }

        private Vector3 GetAimDirection()
        {
            Camera cam = GetActiveCamera();
            Vector3 baseDirection;

            if (cam != null)
                baseDirection = cam.transform.forward;
            else if (firePoint != null)
                baseDirection = firePoint.forward;
            else
                baseDirection = transform.forward;

            Vector3 direction = (baseDirection + Vector3.up * aimVerticalBoost).normalized;
            return direction == Vector3.zero ? Vector3.forward : direction;
        }

        private Camera GetActiveCamera()
        {
            if (playerCamera != null)
                return playerCamera;

            return Camera.main;
        }

        private void SetStatusMessage(string message, float duration)
        {
            _statusMessage = message;
            _statusMessageUntil = Time.time + duration;
        }

        private bool HasRequiredReferences()
        {
            bool isValid = true;

            if (predictor == null)
            {
                Debug.LogError("[TrajectoryTestDriver] predictor 未赋值。请在 Inspector 中拖入 TrajectoryPredictor。");
                isValid = false;
            }

            if (rendererObj == null)
            {
                Debug.LogError("[TrajectoryTestDriver] rendererObj 未赋值。请在 Inspector 中拖入 TrajectoryRenderer。");
                isValid = false;
            }

            if (firePoint == null && GetActiveCamera() == null)
            {
                Debug.LogError("[TrajectoryTestDriver] 当前既没有玩家相机，也没有备用 firePoint。至少需要一个投掷起点。");
                isValid = false;
            }

            return isValid;
        }
    }

    public enum FruitHitReaction
    {
        Drop,
        Hide
    }

    public class RuntimeProjectile : MonoBehaviour
    {
        private TrajectoryTestDriver _owner;

        public bool WasResolved { get; private set; }

        public void Initialize(TrajectoryTestDriver owner, float lifetime)
        {
            _owner = owner;
            Destroy(gameObject, lifetime);
        }

        public void MarkAsResolved()
        {
            WasResolved = true;
        }

        private void OnDestroy()
        {
            if (_owner != null)
                _owner.NotifyProjectileExpired(this);
        }
    }

    public class SimpleFruitTarget : MonoBehaviour
    {
        private TrajectoryTestDriver _owner;
        private bool _wasHit;
        private FruitHitReaction _hitReaction;
        private float _respawnDelay;
        private Vector3 _spawnPosition;
        private Rigidbody _rigidbody;
        private Renderer[] _renderers;
        private Collider[] _colliders;

        public int Points { get; private set; }

        public void Initialize(
            TrajectoryTestDriver owner,
            int points,
            FruitHitReaction hitReaction,
            float respawnDelay,
            Vector3 spawnPosition)
        {
            _owner = owner;
            Points = points;
            _hitReaction = hitReaction;
            _respawnDelay = respawnDelay;
            _spawnPosition = spawnPosition;
            _wasHit = false;

            _rigidbody = GetComponent<Rigidbody>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);

            ResetFruitState();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_wasHit) return;

            if (!collision.gameObject.TryGetComponent(out RuntimeProjectile projectile))
                return;

            _wasHit = true;
            _owner?.RegisterFruitHit(this, projectile, collision);
            HandleHitReaction(collision);
        }

        private void HandleHitReaction(Collision collision)
        {
            switch (_hitReaction)
            {
                case FruitHitReaction.Drop:
                    ApplyDropReaction(collision);
                    break;
                case FruitHitReaction.Hide:
                    ApplyHideReaction();
                    break;
            }
        }

        private void ApplyDropReaction(Collision collision)
        {
            if (_rigidbody == null)
                _rigidbody = gameObject.AddComponent<Rigidbody>();

            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
            _rigidbody.mass = 0.35f;

            Vector3 impulse = collision.relativeVelocity * 0.08f;
            _rigidbody.AddForce(impulse, ForceMode.Impulse);
        }

        private void ApplyHideReaction()
        {
            SetVisualEnabled(false);

            if (_respawnDelay > 0f)
                Invoke(nameof(ResetFruitState), _respawnDelay);
        }

        private void ResetFruitState()
        {
            CancelInvoke(nameof(ResetFruitState));
            _wasHit = false;
            transform.position = _spawnPosition;
            transform.rotation = Quaternion.identity;

            if (_rigidbody != null)
            {
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.isKinematic = _hitReaction != FruitHitReaction.Drop;
                _rigidbody.useGravity = false;
            }

            SetVisualEnabled(true);
        }

        private void SetVisualEnabled(bool visible)
        {
            if (_renderers != null)
            {
                foreach (Renderer rendererComponent in _renderers)
                    rendererComponent.enabled = visible;
            }

            if (_colliders != null)
            {
                foreach (Collider colliderComponent in _colliders)
                    colliderComponent.enabled = visible;
            }
        }
    }
}
