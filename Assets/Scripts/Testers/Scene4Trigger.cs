using StarlightCollect;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class Scene4Trigger : MonoBehaviour
{
    [SerializeField] private PlayableDirector openingDirector;
    [SerializeField] private GameObject portal;
    [SerializeField] private PickupGuidanceEffect pickup;
    [SerializeField] private Transform transportTarget;

    private void OnTriggerEnter(Collider other)
    {
        var t = other.GetComponentInParent<PortalTraveller>();
        if (t)
        {
            openingDirector.Play();
            portal.SetActive(false);
            if (pickup.IsGuidanceVisible())
            {
                TransportLantern(pickup);
            }
            gameObject.SetActive(false);
        }
    }

    private void TransportLantern(PickupGuidanceEffect pickup)
    {
        if (pickup == null || transportTarget == null)
            return;

        pickup.transform.position = transportTarget.position;
        pickup.transform.rotation = transportTarget.rotation;
    }
}
