using System.Collections;
using Core.Utils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace GiftCatch.Shot
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Shotter : GrabtableBase
    {
        [SerializeField] private Transform ShotTransform;
        [SerializeField] private float ShotMaxDistance = 30f;
        [SerializeField] private float ShotCoolDown = 0.25f;
        [SerializeField] private LayerMask ShottableLayMask = ~0;

        [Header("Shot Visual")]
        [SerializeField] private LineRenderer shotLine;
        [SerializeField] private Material shotLineMaterial;
        [SerializeField] private Color shotLineColor = Color.cyan;
        [SerializeField] private float shotLineWidth = 0.015f;
        [SerializeField] private float shotLineDuration = 0.08f;

        private float _nextShotTime;
        private Coroutine _shotLineCoroutine;
        private Material _runtimeShotLineMaterial;

        protected override void Start()
        {
            base.Start();
            EnsureShotLine();
            HideShotLine();
        }

        protected override void OnActivated(ActivateEventArgs args)
        {
            if (Time.time < _nextShotTime)
            {
                return;
            }

            _nextShotTime = Time.time + ShotCoolDown;

            Transform shotOrigin = ShotTransform != null ? ShotTransform : transform;
            Vector3 origin = shotOrigin.position;
            Vector3 direction = shotOrigin.forward;
            bool hasHit = Physics.Raycast(
                    origin,
                    direction,
                    out RaycastHit hit,
                    ShotMaxDistance,
                    ShottableLayMask);

            Vector3 endPoint = hasHit ? hit.point : origin + direction * ShotMaxDistance;
            PlayShotLine(origin, endPoint);

            if (!hasHit)
            {
                return;
            }

            IShottable shottable = hit.collider.GetComponentInParent<IShottable>();
            shottable?.OnShot(hit);
        }

        private void OnDestroy()
        {
            if (_runtimeShotLineMaterial != null)
            {
                Destroy(_runtimeShotLineMaterial);
            }
        }

        private void EnsureShotLine()
        {
            if (shotLine == null)
            {
                GameObject lineObject = new GameObject("Shot Line");
                lineObject.transform.SetParent(transform, false);
                shotLine = lineObject.AddComponent<LineRenderer>();
            }

            shotLine.positionCount = 2;
            shotLine.useWorldSpace = true;
            shotLine.startWidth = shotLineWidth;
            shotLine.endWidth = shotLineWidth;
            shotLine.startColor = shotLineColor;
            shotLine.endColor = shotLineColor;
            shotLine.numCapVertices = 4;

            if (shotLineMaterial != null)
            {
                shotLine.sharedMaterial = shotLineMaterial;
            }
            else if (shotLine.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _runtimeShotLineMaterial = new Material(shader);
                    shotLine.sharedMaterial = _runtimeShotLineMaterial;
                }
            }
        }

        private void PlayShotLine(Vector3 startPoint, Vector3 endPoint)
        {
            EnsureShotLine();

            shotLine.SetPosition(0, startPoint);
            shotLine.SetPosition(1, endPoint);
            shotLine.enabled = true;

            if (_shotLineCoroutine != null)
            {
                StopCoroutine(_shotLineCoroutine);
            }

            _shotLineCoroutine = StartCoroutine(HideShotLineAfterDelay());
        }

        private IEnumerator HideShotLineAfterDelay()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, shotLineDuration));
            HideShotLine();
            _shotLineCoroutine = null;
        }

        private void HideShotLine()
        {
            if (shotLine != null)
            {
                shotLine.enabled = false;
            }
        }
    }
}
