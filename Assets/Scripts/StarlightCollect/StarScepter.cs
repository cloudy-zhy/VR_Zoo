using System.Collections;
using Core.Utils;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;

namespace StarlightCollect
{
    /// <summary>
    /// 星光法杖。发射起点沿用法杖端点，方向使用主摄像机 forward。
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class StarScepter : GrabbableBase
    {
        [Header("Shot")]
        [FormerlySerializedAs("ShotTransform")]
        [SerializeField] private Transform shotTransform;
        [FormerlySerializedAs("ShotMaxDistance")]
        [SerializeField] private float shotMaxDistance = 30f;
        [FormerlySerializedAs("ShotCoolDown")]
        [SerializeField] private float shotCoolDown = 0.25f;
        [FormerlySerializedAs("ShottableLayMask")]
        [SerializeField] private LayerMask shottableLayerMask = ~0;

        [Header("Land Marker")]
        [SerializeField] private LayerMask markerLayerMask = ~0;
        [SerializeField] private GameObject landMarker;
        [SerializeField] private float generatedLandMarkerScale = 0.18f;
        [SerializeField] private Color generatedLandMarkerColor = Color.cyan;

        [Header("Shot Visual")]
        [SerializeField] private LineRenderer shotLine;
        [SerializeField] private Material shotLineMaterial;
        [SerializeField] private Color shotLineColor = Color.cyan;
        [SerializeField] private float shotLineWidth = 0.015f;
        [SerializeField] private float shotLineDuration = 0.08f;

        private XRGrabInteractable _grabInteractable;
        private float _nextShotTime;
        private bool _isHeld;
        private Coroutine _shotLineCoroutine;
        private Material _runtimeShotLineMaterial;
        private Material _runtimeLandMarkerMaterial;
        private Transform _camTran;

        protected override void Start()
        {
            base.Start();

            _grabInteractable = GetComponent<XRGrabInteractable>();
            _grabInteractable.selectEntered.AddListener(OnSelectEntered);
            _grabInteractable.selectExited.AddListener(OnSelectExited);

            EnsureShotLine();
            EnsureLandMarker();
            HideShotLine();
            SetLandMarkerVisible(false);
            _camTran = Camera.main.transform;
        }

        private void Update()
        {
            if (_isHeld)
            {
                UpdateLandMarker();
                return;
            }

            SetLandMarkerVisible(false);
        }

        protected override void OnActivated(ActivateEventArgs args)
        {
            if (Time.time < _nextShotTime)
                return;

            _nextShotTime = Time.time + shotCoolDown;

            Vector3 origin = shotTransform.position;
            Vector3 direction = _camTran.forward;
            bool hasHit = Physics.Raycast(
                origin,
                direction,
                out RaycastHit hit,
                shotMaxDistance,
                shottableLayerMask
            );

            Vector3 endPoint = hasHit ? hit.point : origin + direction * shotMaxDistance;
            PlayShotLine(origin, endPoint);

            if (!hasHit)
                return;

            IShottable shottable = hit.collider.GetComponentInParent<IShottable>();
            shottable?.OnShot(hit);
        }

        private void OnDestroy()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
                _grabInteractable.selectExited.RemoveListener(OnSelectExited);
            }

            if (_runtimeShotLineMaterial != null)
                Destroy(_runtimeShotLineMaterial);

            if (_runtimeLandMarkerMaterial != null)
                Destroy(_runtimeLandMarkerMaterial);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            _isHeld = true;
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            _isHeld = false;
            SetLandMarkerVisible(false);
        }

        private void UpdateLandMarker()
        {
            EnsureLandMarker();

            Vector3 origin = shotTransform.position;
            Vector3 direction = _camTran.forward;

            if (!Physics.Raycast(origin, direction, out RaycastHit hit, shotMaxDistance, markerLayerMask))
            {
                SetLandMarkerVisible(false);
                return;
            }

            landMarker.transform.position = hit.point + hit.normal * 0.02f;
            landMarker.transform.rotation = Quaternion.LookRotation(Vector3.forward, hit.normal);
            SetLandMarkerVisible(true);
        }

        private void EnsureLandMarker()
        {
            if (landMarker != null)
                return;

            landMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            landMarker.name = "StarScepter Land Marker";
            landMarker.transform.SetParent(transform, false);
            landMarker.transform.localScale = Vector3.one * generatedLandMarkerScale;

            Collider markerCollider = landMarker.GetComponent<Collider>();
            if (markerCollider != null)
                markerCollider.enabled = false;

            Renderer markerRenderer = landMarker.GetComponent<Renderer>();
            Shader shader = Shader.Find("Sprites/Default");
            if (markerRenderer != null && shader != null)
            {
                _runtimeLandMarkerMaterial = new Material(shader)
                {
                    color = generatedLandMarkerColor
                };
                markerRenderer.sharedMaterial = _runtimeLandMarkerMaterial;
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
                StopCoroutine(_shotLineCoroutine);

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
                shotLine.enabled = false;
        }

        private void SetLandMarkerVisible(bool visible)
        {
            if (landMarker != null)
                landMarker.SetActive(visible);
        }
    }
}
