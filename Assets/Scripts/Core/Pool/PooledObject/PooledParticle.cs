using System;
using System.Collections;
using Manager;
using UnityEngine;

namespace Core.Pool.PooledObject
{
    public class PooledParticle : MonoBehaviour
    {
        [SerializeField] private ParticleSystem ps;
        [SerializeField] private float duration;

        private void Awake()
        {
            if (ps == null)
                ps = GetComponent<ParticleSystem>();
            duration = duration > 0f ? duration : ps.main.duration + ps.main.startLifetime.constantMax + 0.25f;
        }

        private void OnEnable()
        {
            ps.Play();
            StartCoroutine(ReturnRoutine());
        }

        private IEnumerator ReturnRoutine()
        {
            yield return new WaitForSeconds(duration);
            GameManager.Pool.Return(gameObject);
        }

        private void OnDisable()
        {
            ps.Stop();
        }
    }
}