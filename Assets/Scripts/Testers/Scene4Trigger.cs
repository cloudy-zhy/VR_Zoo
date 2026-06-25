using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class Scene4Trigger : MonoBehaviour
{
    [SerializeField] private PlayableDirector openingDirector;
    private void OnTriggerEnter(Collider other)
    {
        var t = other.GetComponentInParent<PortalTraveller>();
        if (t)
        {
            openingDirector.Play();
            gameObject.SetActive(false);
        }
    }
}
