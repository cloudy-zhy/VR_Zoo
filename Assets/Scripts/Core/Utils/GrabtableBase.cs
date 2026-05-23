using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Core.Utils
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class GrabtableBase : MonoBehaviour
    {
        protected virtual void Start()
        {
            var grab = GetComponent<XRGrabInteractable>();
            grab.activated.AddListener(OnActivated);
            grab.deactivated.AddListener(OnDeactivated);
        }

        protected virtual void OnActivated(ActivateEventArgs args) { }
        protected virtual void OnDeactivated(DeactivateEventArgs args) { }
    }
}