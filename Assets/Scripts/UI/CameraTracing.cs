using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraTracing : MonoBehaviour
{
    [SerializeField] Transform targetTranssform;

    [SerializeField] private float smoothing = 0.1f;

    private void LateUpdate()
    {
        if (targetTranssform != null)
        {
            Vector3 targetPos = targetTranssform.position;
            transform.position = Vector3.Lerp(transform.position, targetPos, smoothing);
        }
    }
}
