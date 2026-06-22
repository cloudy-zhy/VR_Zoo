using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Core.Utils
{
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class GrabbableBase : MonoBehaviour
    {
        protected virtual void Start()
        {
            var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            grab.activated.AddListener(OnActivated);
            grab.deactivated.AddListener(OnDeactivated);
        }

        protected virtual void OnActivated(ActivateEventArgs args) { }
        protected virtual void OnDeactivated(DeactivateEventArgs args) { }
    }
}