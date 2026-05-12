using System.Collections.Generic;
using UnityEngine;

public class HandGestureInteraction2 : MonoBehaviour
{
    [SerializeField] private float debounceInterval = 0.15f;

    private readonly Dictionary<IceChime, float> lastHitTime = new Dictionary<IceChime, float>();

    private void OnTriggerEnter(Collider other)
    {
        IceChime chime = other.GetComponentInParent<IceChime>();
        if (chime == null) return;

        if (lastHitTime.TryGetValue(chime, out float lastTime))
            if (Time.time - lastTime < debounceInterval) return;

        lastHitTime[chime] = Time.time;
        chime.Interact(gameObject);
    }
}