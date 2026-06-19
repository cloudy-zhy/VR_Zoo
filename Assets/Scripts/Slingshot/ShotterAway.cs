using UnityEngine;

namespace Slingshot
{
    public class ShotterAway : MonoBehaviour
    {
        [SerializeField] private Animator ani;
        [SerializeField] private string stateName;
        private bool m_played;

        private void OnTriggerEnter(Collider other)
        {
            if (!m_played && other.gameObject.CompareTag("Train"))
            {
                ani.Play(stateName);
                m_played = true;
            }
        }
    }
}