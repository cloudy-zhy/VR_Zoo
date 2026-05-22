using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Core.Utils
{
    public abstract class GrabtableBase : MonoBehaviour
    {
        protected virtual void Start()
        {
            var grab = GetComponent<XRGrabInteractable>();
            grab.activated.AddListener(OnActivated);
            grab.deactivated.AddListener(OnDeactivated);
        }

        protected abstract void OnActivated(ActivateEventArgs args);
        protected abstract void OnDeactivated(DeactivateEventArgs args);
    }
}