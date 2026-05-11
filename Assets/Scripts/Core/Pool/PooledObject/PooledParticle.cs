using System;
using UnityEngine;

namespace Core.Pool.PoolObjects
{
    public class PooledParticle : MonoBehaviour
    {
        [SerializeField] private ParticleSystem ps;
        public ParticleSystem ParticleSystem => ps;
        [SerializeField] private float duration;
        public float Duration => duration;

        private void Awake()
        {
            if (ps == null)
                ps = GetComponent<ParticleSystem>();
            duration = duration > 0f ? duration : ps.main.duration + ps.main.startLifetime.constantMax + 0.25f;
            if (ps != null)
            {
                ps.Stop();
            }
        }

        private void OnEnable()
        {
            ps.Play();
        }

        private void OnDisable()
        {
            ps.Stop();
        }
    }
}