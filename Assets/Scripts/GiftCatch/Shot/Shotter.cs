using Core.Utils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace GiftCatch.Shot
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Shotter : GrabtableBase
    {
        [SerializeField] private Transform ShotTransform;
        [SerializeField] private float ShotMaxDistance = 30f;
        [SerializeField] private float ShotCoolDown = 0.25f;
        [SerializeField] private LayerMask ShottableLayMask = ~0;

        private float _nextShotTime;

        protected override void OnActivated(ActivateEventArgs args)
        {
            if (Time.time < _nextShotTime)
            {
                return;
            }

            _nextShotTime = Time.time + ShotCoolDown;

            Transform shotOrigin = ShotTransform != null ? ShotTransform : transform;
            if (!Physics.Raycast(
                    shotOrigin.position,
                    shotOrigin.forward,
                    out RaycastHit hit,
                    ShotMaxDistance,
                    ShottableLayMask))
            {
                return;
            }

            IShottable shottable = hit.collider.GetComponentInParent<IShottable>();
            shottable?.OnShot(hit);
        }

        protected override void OnDeactivated(DeactivateEventArgs args)
        {
        }
    }
}
