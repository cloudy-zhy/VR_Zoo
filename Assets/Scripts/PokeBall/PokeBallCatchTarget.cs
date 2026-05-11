using Core.Pool.PoolObjects;
using DG.Tweening;
using Manager;
using UnityEngine;

namespace PokeBall
{
    /// <summary>
    /// 可被精灵球捕获的目标配置。
    /// 挂载后，精灵球会优先使用本组件提供的表现参数和事件 payload。
    /// </summary>
    public class PokeBallCatchTarget : MonoBehaviour
    {
        [Header("目标")]
        [Tooltip("实际播放缩放/旋转动画的根节点。留空则使用当前 Transform。")]
        [SerializeField] private Transform root;
        // [SerializeField] private Transform targetRoot;
        // [Tooltip("抓中事件优先广播的业务组件。留空则广播本 PokeBallCatchTarget。")]
        // [SerializeField] private Component payloadComponentOverride;

        [Header("动画")]
        [Tooltip("目标 Animator 上被抓中时触发的 Trigger。留空则不触发 Animator。")]
        [SerializeField] private string caughtAnimatorTrigger = "Caught";
        [Tooltip("目标被吸收时的本地旋转轴。")]
        [SerializeField] private Vector3 rotateAxis = Vector3.up;
        [Tooltip("目标被吸收时累计旋转角度。")]
        [SerializeField] private float rotateDegrees = 720f;
        [Tooltip("旋转并缩小的持续时间。")]
        [Min(0f)]
        [SerializeField] private float shrinkDuration = 0.8f;
        public float ShrinkDuration => shrinkDuration;
        [Tooltip("动画结束时的本地缩放。")]
        [SerializeField] private Vector3 finalScale = Vector3.zero;
        [Tooltip("目标专属粒子预制体或场景粒子在对象池系统中的key")]
        [SerializeField] private string targetVfxPrefabKey;
        [Tooltip("完成抓取动画后是否隐藏目标根节点。")]
        [SerializeField] private bool deactivateAfterCaught = true;

        private Sequence _caughtSequence;
        private Animator _animator;

        // /// <summary>
        // /// 实际播放效果的根节点。
        // /// </summary>
        // public Transform EffectRoot => targetRoot != null ? targetRoot : transform;
        //
        // /// <summary>
        // /// 抓中事件广播的 payload。
        // /// </summary>
        // public UnityEngine.Object Payload => payloadComponentOverride != null ? payloadComponentOverride : this;
        
        private void Awake()
        {
            TryGetComponent(out _animator);
            root = root != null ? root : transform;
        }

        private void OnDestroy()
        {
            _caughtSequence?.Kill();
        }

        /// <summary>
        /// 播放目标被抓中的表现，并在完成后回调。
        /// </summary>
        public void PlayCaughtEffect()
        {
            // Transform root = EffectRoot;
            // if (root == null)
            // {
            //     GameManager.Event.Broadcast("PokeBall.Caught", new EventParameter<PokeBallCatchTarget>(this));
            //     return;
            // }

            _caughtSequence?.Kill();
            if (!string.IsNullOrEmpty(caughtAnimatorTrigger) && _animator != null)
            {
                _animator.SetTrigger(caughtAnimatorTrigger);
            }
            if (!string.IsNullOrEmpty(targetVfxPrefabKey))
                PlayParticle(targetVfxPrefabKey, root.position, root.rotation, root);

            if (shrinkDuration <= 0f)
            {
                root.localScale = finalScale;
                GameManager.Event.Broadcast("PokeBall.Caught", this);
                DeactivateRootIfNeeded(root);
                return;
            }

            Vector3 rotateVector = rotateAxis.sqrMagnitude > 0.001f
                ? rotateAxis.normalized * rotateDegrees
                : Vector3.up * rotateDegrees;

            _caughtSequence = DOTween.Sequence()
                .Join(root.DOLocalRotate(rotateVector, shrinkDuration, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.InOutSine))
                .Join(root.DOScale(finalScale, shrinkDuration)
                    .SetEase(Ease.InBack))
                .OnComplete(() =>
                {
                    GameManager.Event.Broadcast("PokeBall.Caught", this);
                    DeactivateRootIfNeeded(root);
                })
                .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy);
        }

        // private void PlayAnimator(Transform root)
        // {
        //     if (string.IsNullOrEmpty(caughtAnimatorTrigger))
        //     {
        //         return;
        //     }
        //
        //     Animator animator = root.GetComponentInChildren<Animator>();
        //     if (animator != null)
        //     {
        //         animator.SetTrigger(caughtAnimatorTrigger);
        //     }
        // }

        private void DeactivateRootIfNeeded(Transform rootTransform)
        {
            if (deactivateAfterCaught && rootTransform != null)
            {
                rootTransform.gameObject.SetActive(false);
            }
        }

        private static void PlayParticle(string targetVfxPrefabKey, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (string.IsNullOrEmpty(targetVfxPrefabKey))
                return;

            PooledParticle particle = GameManager.Pool.Rent<PooledParticle>(targetVfxPrefabKey, position, rotation, parent);
            particle?.ParticleSystem?.Play();
            if (particle != null)
                GameManager.Pool.Return(particle, particle.Duration);
        }
    }
}
