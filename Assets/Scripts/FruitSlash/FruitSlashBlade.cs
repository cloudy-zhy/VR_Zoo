using System.Collections;
using System.Collections.Generic;
using Core.Event;
using Manager;
using UnityEngine;

namespace FruitSlash
{
    /// <summary>
    /// 光芒蕨叶刃。通过采样上一帧到当前帧的移动线段，对果实碰撞盒做无方向限制斩切检测。
    /// </summary>
    public class FruitSlashBlade : MonoBehaviour
    {
        [Header("判定")]
        [SerializeField] private LayerMask fruitMask = ~0;
        [SerializeField] private float baseHitRadius = 0.12f;
        [SerializeField] private float minSegmentDistance = 0.015f;
        [SerializeField] private float swingResetDelay = 0.18f;

        [Header("表现")]
        [Tooltip("刀尾/刀光采样点")]
        [SerializeField] private Transform tailTransform;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private Renderer bladeRenderer;
        [SerializeField] private ParticleSystem empoweredParticles;
        [SerializeField] private Color normalColor = new Color(0.4f, 1f, 0.65f);
        [SerializeField] private Color empoweredColor = new Color(1f, 0.95f, 0.35f);

        [Header("调试")]
        [SerializeField] private bool debugLogHits;

        public float CurrentHitRadius => baseHitRadius * _hitRadiusMultiplier;

        private readonly Collider[] _hitBuffer = new Collider[32];
        private readonly HashSet<FruitSlashFruit> _currentSwingHits = new();
        private Vector3 _lastPosition;
        private float _lastMovementTime;
        private float _hitRadiusMultiplier = 1f;
        private int _sameSwingCutCount;
        private Coroutine _empoweredRoutine;
        private MaterialPropertyBlock _bladePropertyBlock;

        private void Awake()
        {
            _lastPosition = tailTransform.position;
            ApplyEmpoweredVisual(false);
            GameManager.Event.Register<float>(FruitSlashEvents.BladeEmpowered, OnEmpowered);
        }

        private void OnDestroy()
        {
            GameManager.Event.Unregister<float>(FruitSlashEvents.BladeEmpowered, OnEmpowered);
        }

        private void Update()
        {
            Vector3 currentPosition = tailTransform.position;
            Vector3 delta = currentPosition - _lastPosition;

            if (delta.magnitude >= minSegmentDistance)
            {
                DetectFruitHits(_lastPosition, currentPosition);
                _lastMovementTime = Time.time;
            }
            else if (Time.time - _lastMovementTime > swingResetDelay)
            {
                ResetSwing();
            }

            _lastPosition = currentPosition;
        }

        /// <summary>
        /// 临时强化刀光。
        /// </summary>
        private void OnEmpowered(EventContext<float> context)
        {
            float duration = context.Payload;
            if (_empoweredRoutine != null)
            {
                StopCoroutine(_empoweredRoutine);
                _empoweredRoutine = null;
            }

            _empoweredRoutine = StartCoroutine(EmpoweredRoutine(duration));
        }

        /// <summary>
        /// 设置斩切判定半径倍率。
        /// </summary>
        public void SetHitRadiusMultiplier(float multiplier)
        {
            _hitRadiusMultiplier = Mathf.Max(0.1f, multiplier);
            ApplyLineWidth();
        }

        private IEnumerator EmpoweredRoutine(float duration)
        {
            SetHitRadiusMultiplier(1.8f);
            ApplyEmpoweredVisual(true);
            yield return new WaitForSeconds(Mathf.Max(0f, duration));
            SetHitRadiusMultiplier(1f);
            ApplyEmpoweredVisual(false);
            _empoweredRoutine = null;
        }

        private void DetectFruitHits(Vector3 segmentStart, Vector3 segmentEnd)
        {
            int count = Physics.OverlapCapsuleNonAlloc(
                segmentStart,
                segmentEnd,
                CurrentHitRadius,
                _hitBuffer,
                fruitMask,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < count; i++)
            {
                Collider hit = _hitBuffer[i];
                if (hit == null)
                    continue;

                FruitSlashFruit fruit = hit.GetComponentInParent<FruitSlashFruit>();
                if (fruit == null || _currentSwingHits.Contains(fruit))
                    continue;

                _sameSwingCutCount += 1;
                if (fruit.TryCut(segmentStart, segmentEnd, _sameSwingCutCount))
                {
                    _currentSwingHits.Add(fruit);
                    if (debugLogHits)
                        Debug.Log($"[FruitSlashBlade] {name} hit {fruit.name}, sameSwingCutCount={_sameSwingCutCount}");
                }
                else
                {
                    _sameSwingCutCount = Mathf.Max(0, _sameSwingCutCount - 1);
                }
            }
        }

        private void ResetSwing()
        {
            _currentSwingHits.Clear();
            _sameSwingCutCount = 0;
        }

        private void ApplyEmpoweredVisual(bool empowered)
        {
            Color targetColor = empowered ? empoweredColor : normalColor;

            if (bladeRenderer != null)
                ApplyRendererColor(targetColor);

            if (trail != null)
            {
                trail.startColor = targetColor;
                trail.endColor = new Color(targetColor.r, targetColor.g, targetColor.b, 0f);
            }

            if (empoweredParticles != null)
            {
                if (empowered && !empoweredParticles.isPlaying)
                    empoweredParticles.Play();
                else if (!empowered && empoweredParticles.isPlaying)
                    empoweredParticles.Stop();
            }

            ApplyLineWidth();
        }

        private void ApplyRendererColor(Color targetColor)
        {
            _bladePropertyBlock ??= new MaterialPropertyBlock();
            bladeRenderer.GetPropertyBlock(_bladePropertyBlock);
            _bladePropertyBlock.SetColor("_BaseColor", targetColor);
            _bladePropertyBlock.SetColor("_Color", targetColor);
            bladeRenderer.SetPropertyBlock(_bladePropertyBlock);
        }

        private void ApplyLineWidth()
        {
            float width = CurrentHitRadius * 0.7f;

            if (trail != null)
                trail.widthMultiplier = width;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(tailTransform.position, CurrentHitRadius);
        }
#endif
    }
}
