using Core.Event;
using Core.Utils;
using Entity.Pterosaur;
using Manager;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace StarlightCollect
{
    /// <summary>
    /// 星光提灯
    /// 1.接收翼龙到达的消息，申请回归星光并initialize
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class StarLantern : GrabbableBase
    {
        private float _arrivalDistSqr;

        protected override void Start()
        {
            base.Start();
            _arrivalDistSqr = StarlightConstant.ArrivalDist * StarlightConstant.ArrivalDist;
            GameManager.Event.Register<Pterosaur>(StarlightConstant.PterosaurArrived, OnPterosaurArrived);
        }

        private void OnDestroy()
        {
            GameManager.Event.Unregister<Pterosaur>(StarlightConstant.PterosaurArrived, OnPterosaurArrived);
        }

        private void OnPterosaurArrived(EventContext<Pterosaur> context)
        {
            Vector3 position = context.Payload.transform.position;
            if (GameManager.Pool.TryRent<StarlightCollecting>(StarlightConstant.StarLightCollectingPoolKey,
                    out var starlight, position))
            {
                starlight.Initialize(_arrivalDistSqr, transform);
            }
        }
    }
}
