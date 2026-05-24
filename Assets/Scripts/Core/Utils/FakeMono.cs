using UnityEngine;
using UnityEngine.Events;

namespace Core.Utils
{   
    public class FakeMono : MonoBehaviour
    {
        public UnityEvent onAwake;
        public UnityEvent onStart;
        public UnityEvent onUpdate;

        private void Awake()
        {
            onAwake.Invoke();
        }

        private void Start()
        {
            onStart.Invoke();
        }

        private void Update()
        {
            onUpdate.Invoke();
        }
    }
}