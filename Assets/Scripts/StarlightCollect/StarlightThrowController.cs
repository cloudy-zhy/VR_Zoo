using System.Collections;
using Entity.Pterosaur;
using Manager;
using UnityEngine;

namespace StarlightCollect
{
    public class StarlightThrowController : MonoBehaviour
    {
        [Header("Pterosaur")]
        [SerializeField] private Transform pterosaurParent;

        private int _minRequestCount = 1;
        private int _maxRequestCount = 1;
        private float _spawnInterval = 3;
        private Vector2 _requestSpacingRange = new Vector2(0, 1);
        private float _throwVelocity = 1;

        private bool _isRunning;
        private Coroutine _throwLoopCoroutine;
        private Pterosaur[] _pterosaurs;
        private Transform _starLightParent;
        
        public void ApplyConfig(StarlightLevelSO config)
        {
            _throwVelocity = config.throwVelocity;
            _spawnInterval = config.spawnInterval;
            _minRequestCount = config.minRequestCount;
            _maxRequestCount = config.maxRequestCount;
            _requestSpacingRange = config.requestSpacingRange;
        }

        private void Start()
        {
            GameManager.Event.Register<Vector3>(StarlightConstant.PterosaurThrow, OnPterosaurThrow);

            if (pterosaurParent != null)
                _pterosaurs = pterosaurParent.GetComponentsInChildren<Pterosaur>(true);

            GameObject parentObject = new GameObject("StarLightParent");
            parentObject.transform.SetParent(transform);
            _starLightParent = parentObject.transform;
        }

        private void OnDestroy()
        {
            GameManager.Event.Unregister<Vector3>(StarlightConstant.PterosaurThrow, OnPterosaurThrow);
        }

        [ContextMenu("StartThrowGame")]
        public void StartThrowGame()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _throwLoopCoroutine = StartCoroutine(ThrowLoop());
        }

        [ContextMenu("StopThrowGame")]
        public void StopThrowGame()
        {
            _isRunning = false;

            if (_throwLoopCoroutine == null)
                return;

            StopCoroutine(_throwLoopCoroutine);
            _throwLoopCoroutine = null;
        }

        private IEnumerator ThrowLoop()
        {
            while (_isRunning)
            {
                int requestCount = Random.Range(
                    Mathf.Max(1, _minRequestCount),
                    Mathf.Max(_minRequestCount, _maxRequestCount) + 1
                );

                for (int i = 0; i < requestCount; i++)
                {
                    RequestRandomPterosaurThrow();
                    yield return new WaitForSeconds(Random.Range(
                        Mathf.Min(_requestSpacingRange.x, _requestSpacingRange.y),
                        Mathf.Max(_requestSpacingRange.x, _requestSpacingRange.y)
                    ));
                }

                yield return new WaitForSeconds(Mathf.Max(0.1f, _spawnInterval));
            }
        }

        private void RequestRandomPterosaurThrow()
        {
            if (_pterosaurs == null || _pterosaurs.Length == 0)
                return;

            Pterosaur pterosaur = _pterosaurs[Random.Range(0, _pterosaurs.Length)];
            pterosaur?.AddRequest();
        }

        private void OnPterosaurThrow(Core.Event.EventContext<Vector3> context)
        {
            if (!_isRunning)
                return;

            if (GameManager.Pool.TryRent<StarLight>(StarlightConstant.StarLightPoolKey, out var starLight,
                    position: context.Payload, parent: _starLightParent))
            {
                starLight.Initialize(_throwVelocity);
            }
        }
    }
}
