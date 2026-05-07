using Core.Event;
using Core.Pool;
using Manager;
using UnityEngine;

namespace FruitSlash
{
    /// <summary>
    /// 可被光刃斩切的果实运行时对象。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class FruitSlashFruit : PoolableObject
    {
        [Header("默认配置")]
        [SerializeField] private FruitSlashFruitType fruitType = FruitSlashFruitType.FlameEgg;
        [SerializeField] private int baseScore = 15;
        [SerializeField] private bool isRare;
        [SerializeField] private bool isFast;
        [SerializeField] private bool isRainbowBunch;

        [Header("七彩巨大果串")]
        [SerializeField] private int requiredRainbowHits = 3;
        [SerializeField] private int rainbowReward = 150;

        [Header("反馈")]
        [SerializeField] private string halfFruitPoolKey;
        [SerializeField] private string juiceVfxPoolKey;
        [SerializeField] private string sparkVfxPoolKey;
        [SerializeField] private AudioClip cutAudio;
        [SerializeField] private float halfImpulse = 1.8f;
        [SerializeField] private float halfLifeTime = 3f;

        [Header("落地判定")]
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private float failSafeY = -1f;

        public FruitSlashFruitType FruitType => fruitType;
        public int BaseScore => baseScore;
        public bool IsRare => isRare;
        public bool IsFast => isFast;
        public bool IsRainbowBunch => isRainbowBunch;
        public int RainbowReward => rainbowReward;
        public bool IsFinished => _cutFinished || _missed;

        private Rigidbody _rb;
        private Collider _collider;
        private Renderer[] _renderers;
        private Color _placeholderColor;
        private bool _cutFinished;
        private bool _missed;
        private int _rainbowHitCount;

        private void Awake()
        {
            CacheComponents();
            if (groundMask.value == 0)
                groundMask = LayerMask.GetMask("Land");
        }

        private void Update()
        {
            if (!_cutFinished && !_missed && transform.position.y < failSafeY)
                Miss();
        }

        /// <summary>
        /// 初始化运行时果实。
        /// </summary>
        public void Initialize(
            FruitSlashFruitConfigSO config,
            FruitSlashFruitType type,
            bool rare,
            bool fast,
            bool rainbow,
            Vector3 velocity,
            string fallbackHalfPoolKey)
        {
            CacheComponents();

            fruitType = type;
            isRare = rare;
            isFast = fast;
            isRainbowBunch = rainbow;
            _cutFinished = false;
            _missed = false;
            _rainbowHitCount = 0;

            if (config != null)
            {
                baseScore = config.baseScore;
                halfFruitPoolKey = config.halfFruitPoolKey;
                juiceVfxPoolKey = config.juiceVfxPoolKey;
                sparkVfxPoolKey = config.sparkVfxPoolKey;
                cutAudio = config.cutAudio;
                _placeholderColor = config.placeholderColor;
            }
            else
            {
                baseScore = FruitSlashFruitConfigSO.GetDefaultScore(type);
                _placeholderColor = FruitSlashFruitConfigSO.GetDefaultColor(type);
                halfFruitPoolKey = fallbackHalfPoolKey;
                juiceVfxPoolKey = string.Empty;
                sparkVfxPoolKey = string.Empty;
                cutAudio = null;
            }

            if (isRare)
                baseScore = 50;
            if (isRainbowBunch)
                baseScore = rainbowReward;

            SetRenderersVisible(true);
            ApplyPlaceholderColor();

            _collider.enabled = true;
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.velocity = velocity;
            _rb.angularVelocity = Random.insideUnitSphere * 2f;
        }

        /// <summary>
        /// 由光刃调用。返回 true 表示这次挥刀已命中该果实，应计入当前挥刀命中集合。
        /// </summary>
        public bool TryCut(FruitSlashBlade blade, Vector3 segmentStart, Vector3 segmentEnd, int sameSwingCutCount)
        {
            if (_cutFinished || _missed)
                return false;

            if (isRainbowBunch)
            {
                _rainbowHitCount += 1;
                SpawnHitFeedback(segmentStart, segmentEnd);

                if (_rainbowHitCount < requiredRainbowHits)
                    return true;
            }

            FinishCut(blade, segmentStart, segmentEnd, sameSwingCutCount);
            return true;
        }

        private void FinishCut(FruitSlashBlade blade, Vector3 segmentStart, Vector3 segmentEnd, int sameSwingCutCount)
        {
            _cutFinished = true;
            _collider.enabled = false;
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;

            Vector3 splitDirection = CalculateSplitDirection(segmentStart, segmentEnd);
            SpawnHalves(splitDirection);
            SpawnHitFeedback(segmentStart, segmentEnd);
            SetRenderersVisible(false);

            if (cutAudio != null)
                AudioSource.PlayClipAtPoint(cutAudio, transform.position);

            GameManager.Event.Broadcast(FruitSlashEvents.InternalFruitCut,this, blade, sameSwingCutCount);
            ReturnToPool();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_cutFinished || _missed)
                return;

            if ((groundMask.value & (1 << collision.gameObject.layer)) == 0)
                return;

            Miss();
        }

        private void Miss()
        {
            if (_cutFinished || _missed)
                return;

            _missed = true;
            GameManager.Event.Broadcast(FruitSlashEvents.InternalFruitMissed, this);
            ReturnToPool();
        }

        private void SpawnHalves(Vector3 splitDirection)
        {
            Vector3 center = transform.position;
            Quaternion rotation = transform.rotation;

            GameObject left = CreateHalf(center + splitDirection * 0.08f, rotation, "Half_A");
            GameObject right = CreateHalf(center - splitDirection * 0.08f, rotation, "Half_B");

            ApplyHalfImpulse(left, splitDirection);
            ApplyHalfImpulse(right, -splitDirection);
        }

        private GameObject CreateHalf(Vector3 position, Quaternion rotation, string suffix)
        {
            GameObject half = null;
            FruitSlashPooledObject pooledHalf = null;

            if (!string.IsNullOrEmpty(halfFruitPoolKey) && PoolManager.I != null && PoolManager.I.HasPool(halfFruitPoolKey))
            {
                pooledHalf = PoolManager.I.Get<FruitSlashPooledObject>(halfFruitPoolKey, position, rotation);
                half = pooledHalf != null ? pooledHalf.gameObject : null;
            }

            if (half == null)
                return null;

            if (half.GetComponent<Collider>() == null)
                half.AddComponent<SphereCollider>();

            Rigidbody halfRb = half.GetComponent<Rigidbody>();
            if (halfRb == null)
                halfRb = half.AddComponent<Rigidbody>();

            halfRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            half.transform.localScale = transform.localScale * 0.45f;
            ApplyColorToRenderers(half, _placeholderColor);

            if (pooledHalf != null)
                PoolManager.I.Return(pooledHalf, halfLifeTime);

            return half;
        }

        private void ApplyHalfImpulse(GameObject half, Vector3 direction)
        {
            if (half == null)
                return;

            Rigidbody halfRb = half.GetComponent<Rigidbody>();
            if (halfRb == null)
                return;

            Vector3 impulse = (direction.normalized + Vector3.up * 0.35f) * halfImpulse;
            halfRb.AddForce(impulse, ForceMode.Impulse);
            halfRb.AddTorque(Random.insideUnitSphere * halfImpulse, ForceMode.Impulse);
        }

        private void SpawnHitFeedback(Vector3 segmentStart, Vector3 segmentEnd)
        {
            Quaternion rotation = Quaternion.LookRotation((segmentEnd - segmentStart).normalized, Vector3.up);
            SpawnTimedPoolObject(juiceVfxPoolKey, transform.position, rotation, 3f);
            SpawnTimedPoolObject(sparkVfxPoolKey, transform.position, rotation, 3f);
        }

        private Vector3 CalculateSplitDirection(Vector3 segmentStart, Vector3 segmentEnd)
        {
            Vector3 bladeDirection = segmentEnd - segmentStart;
            Vector3 toFruit = transform.position - segmentStart;
            Vector3 splitDirection = Vector3.Cross(bladeDirection, toFruit);
            if (splitDirection.sqrMagnitude < 0.0001f)
                splitDirection = Vector3.Cross(bladeDirection, Vector3.up);
            if (splitDirection.sqrMagnitude < 0.0001f)
                splitDirection = transform.right;
            return splitDirection.normalized;
        }

        private void CacheComponents()
        {
            if (_rb == null)
                _rb = GetComponent<Rigidbody>();
            if (_collider == null)
                _collider = GetComponent<Collider>();
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void SetRenderersVisible(bool visible)
        {
            CacheComponents();
            foreach (Renderer fruitRenderer in _renderers)
            {
                if (fruitRenderer != null)
                    fruitRenderer.enabled = visible;
            }
        }

        private void ApplyPlaceholderColor()
        {
            CacheComponents();
            foreach (Renderer fruitRenderer in _renderers)
            {
                if (fruitRenderer != null)
                    fruitRenderer.material.color = _placeholderColor;
            }
        }

        private static void ApplyColorToRenderers(GameObject target, Color color)
        {
            Renderer[] targetRenderers = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null)
                    targetRenderers[i].material.color = color;
            }
        }

        private static void SpawnTimedPoolObject(string key, Vector3 position, Quaternion rotation, float lifetime)
        {
            if (string.IsNullOrEmpty(key) || PoolManager.I == null || !PoolManager.I.HasPool(key))
                return;

            PoolableObject obj = PoolManager.I.Get(key, position, rotation);
            if (obj != null)
                PoolManager.I.Return(obj, lifetime);
        }

        public override void OnReturnToPool()
        {
            if (_rb != null)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
            }
            if (_collider != null)
                _collider.enabled = false;
            SetRenderersVisible(true);
            base.OnReturnToPool();
        }
    }
}
