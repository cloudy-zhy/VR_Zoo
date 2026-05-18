using Core.Event;
using Manager;
using UnityEngine;

namespace FruitSlash
{
    /// <summary>
    /// 可被光刃斩切的果实运行时对象。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class FruitSlashFruit : MonoBehaviour
    {
        [Header("默认配置")]
        [SerializeField] private FruitSlashFruitType fruitType = FruitSlashFruitType.FlameEgg;
        [SerializeField] private int baseScore = 15;
        [SerializeField] private bool isRare;
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
        public bool IsRainbowBunch => isRainbowBunch;
        public int RainbowReward => rainbowReward;
        /// <summary>本次挥刀连续切中的果实数量。</summary>
        public int LastSameSwingCutCount { get; private set; }

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
            bool rainbow,
            Vector3 velocity,
            string fallbackHalfPoolKey)
        {
            CacheComponents();

            fruitType = type;
            isRare = rare;
            isRainbowBunch = rainbow;
            _cutFinished = false;
            _missed = false;
            _rainbowHitCount = 0;
            LastSameSwingCutCount = 0;

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
        public bool TryCut(Vector3 segmentStart, Vector3 segmentEnd, int sameSwingCutCount)
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

            FinishCut(segmentStart, segmentEnd, sameSwingCutCount);
            return true;
        }

        private void FinishCut(Vector3 segmentStart, Vector3 segmentEnd, int sameSwingCutCount)
        {
            _cutFinished = true;
            _collider.enabled = false;
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            LastSameSwingCutCount = sameSwingCutCount;

            Vector3 splitDirection = CalculateSplitDirection(segmentStart, segmentEnd);
            SpawnHalves(splitDirection);
            SpawnHitFeedback(segmentStart, segmentEnd);
            SetRenderersVisible(false);

            if (cutAudio != null)
                AudioSource.PlayClipAtPoint(cutAudio, transform.position);

            this.Broadcast(FruitSlashEvents.InternalFruitCut, this);
            GameManager.Pool.Return(this);
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
            this.Broadcast(FruitSlashEvents.InternalFruitMissed, this);
            GameManager.Pool.Return(this);
        }

        private void SpawnHalves(Vector3 splitDirection)
        {
            Vector3 center = transform.position;
            Quaternion rotation = transform.rotation;

            GameObject left = CreateHalf(center + splitDirection * 0.08f, rotation);
            GameObject right = CreateHalf(center - splitDirection * 0.08f, rotation);

            ApplyHalfImpulse(left, splitDirection);
            ApplyHalfImpulse(right, -splitDirection);
        }

        private GameObject CreateHalf(Vector3 position, Quaternion rotation)
        {
            GameObject half = null;
            FruitSlashPooledObject pooledHalf = null;

            if (!string.IsNullOrEmpty(halfFruitPoolKey))
            {
                pooledHalf = GameManager.Pool.Rent<FruitSlashPooledObject>(halfFruitPoolKey, position, rotation);
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
                GameManager.Pool.Return(pooledHalf, halfLifeTime);

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
            GameManager.Pool.Rent(juiceVfxPoolKey, 3f, transform.position, rotation).Forget();
            GameManager.Pool.Rent(sparkVfxPoolKey, 3f, transform.position, rotation).Forget();
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
            foreach (var renderer in targetRenderers)
            {
                if (renderer != null)
                    renderer.material.color = color;
            }
        }

        public void OnDisable()
        {
            if (_rb != null && !_rb.isKinematic)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
            }
            if (_collider != null)
                _collider.enabled = false;
            SetRenderersVisible(true);
        }
    }
}
