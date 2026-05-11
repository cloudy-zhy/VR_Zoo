using System;
using System.Collections;
using Core.Pool.PoolObjects;
using DG.Tweening;
using Manager;
using UnityEngine;

namespace PokeBall
{
    /// <summary>
    /// 精灵球投掷物。负责手持、飞行、命中判定、抓取表现和事件广播。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class PokeBallProjectile : MonoBehaviour
    {
        private enum ProjectileState
        {
            Idle,
            Held,
            Flying,
            Catching
        }

        [Header("精灵球表现")]
        [Tooltip("命中后精灵球浮空的位置偏移。")]
        [SerializeField] private Vector3 hoverOffset = new Vector3(0f, 0.4f, 0f);
        [Tooltip("精灵球 Animator 上命中时触发的 Trigger。留空则不触发 Animator。")]
        [SerializeField] private string ballAnimatorTrigger = "Catch";
        [Tooltip("精灵球命中时播放的粒子。可以是球预制体上的场景粒子，也可以是粒子预制体。")]
        [SerializeField] private string targetVfxPrefabKey;
        [Tooltip("未命中可捕获目标时，飞行精灵球自动销毁的时间。")]
        [Min(0f)]
        [SerializeField] private float missCleanupTime = 8f;

        [Header("动画")]
        [Tooltip("本地旋转轴。")]
        [SerializeField] private Vector3 rotateAxis = Vector3.up;
        [Tooltip("累计旋转角度。")]
        [SerializeField] private float rotateDegrees = 720f;
        [Tooltip("放大的持续时间，至少大于目标物体被抓捕的动画时间")]
        [Min(0f)]
        [SerializeField] private float showDuration = 3f;
        [Tooltip("缩小的持续时间")]
        [Min(0f)]
        [SerializeField] private float closeDuration = 3f;
        [Tooltip("精灵球变大的缩放")]
        [SerializeField] private Vector3 finalScale = Vector3.one;

        private Rigidbody _rb;
        private Collider _collider;
        private Animator _animator;
        private ProjectileState _state = ProjectileState.Idle;
        private Coroutine _cleanupCoroutine;
        private Sequence _throwSequence;
        // private Sequence _defaultTargetSequence;

        private void Awake()
        {
            _rb       = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _animator = GetComponent<Animator>();
            
            _rb.useGravity = false;
            _rb.isKinematic = true;
            _collider.enabled = false;
        }

        // private void OnDestroy()
        // {
        //     _throwSequence?.Kill();
        // }

        /// <summary>
        /// 将精灵球吸附到手部锚点，并关闭物理和碰撞。
        /// </summary>
        public void AttachToHand(Transform handAnchor)
        {
            if (!handAnchor)
            {
                return;
            }

            StopCleanupTimer();
            _state = ProjectileState.Held;
            _rb.useGravity = false;
            _rb.isKinematic = true;
            _collider.enabled = false;
            transform.SetParent(handAnchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// 解除手部吸附并按给定速度投掷。
        /// </summary>
        public void Throw(Vector3 velocity)
        {
            transform.SetParent(null, true);
            _state = ProjectileState.Flying;

            _collider.enabled = true;
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.velocity = velocity;
            _rb.angularVelocity = Vector3.zero;

            if (missCleanupTime > 0f)
            {
                _cleanupCoroutine = StartCoroutine(DestroyAfterMiss());
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_state != ProjectileState.Flying || collision.collider == null)
            {
                return;
            }

            Vector3 contactPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.collider.ClosestPoint(transform.position);
            if (collision.gameObject.TryGetComponent(out PokeBallCatchTarget catchTarget))
            {
                _state = ProjectileState.Catching;
                StopCleanupTimer();

                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.useGravity = false;
                _rb.isKinematic = true;
                _collider.enabled = false;
                transform.position = contactPoint + hoverOffset;

                if (_animator != null && !string.IsNullOrEmpty(ballAnimatorTrigger))
                {
                    _animator.SetTrigger(ballAnimatorTrigger);
                }
                PlayParticle(targetVfxPrefabKey, transform.position, transform.rotation, null);

                catchTarget?.PlayCaughtEffect();
                
                float duration = Math.Max(showDuration, catchTarget == null ? 0 : catchTarget.ShrinkDuration);
                Vector3 rotateVector = rotateAxis.sqrMagnitude > 0.001f
                    ? rotateAxis.normalized * rotateDegrees
                    : Vector3.up * rotateDegrees;
                _throwSequence?.Kill();
                _throwSequence = DOTween.Sequence()
                    .Join(transform.DOLocalRotate(rotateVector, duration, RotateMode.LocalAxisAdd)
                        .SetEase(Ease.InOutSine))
                    .Join(transform.DOScale(finalScale, duration)
                        .SetEase(Ease.OutSine))
                    .Append(transform.DOScale(Vector3.zero, closeDuration)
                        .SetEase(Ease.InSine))
                    .OnComplete(() =>
                    {
                        Destroy(gameObject);
                    })
                    .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
        }

        private IEnumerator DestroyAfterMiss()
        {
            yield return new WaitForSeconds(missCleanupTime);
            if (_state == ProjectileState.Flying)
            {
                Destroy(gameObject);
            }
        }

        private void StopCleanupTimer()
        {
            if (_cleanupCoroutine == null)
            {
                return;
            }

            StopCoroutine(_cleanupCoroutine);
            _cleanupCoroutine = null;
        }

        private static void PlayParticle(string targetVfxPrefabKey, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (string.IsNullOrEmpty(targetVfxPrefabKey))
                return;
            
            PooledParticle particle = GameManager.Pool.Rent<PooledParticle>(targetVfxPrefabKey, position, rotation, parent);
            if (particle != null)
                GameManager.Pool.Return(particle, particle.Duration);
        }
    }
}