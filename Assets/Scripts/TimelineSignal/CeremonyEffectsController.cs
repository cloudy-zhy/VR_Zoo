using UnityEngine;
using UnityEngine.Playables;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TimelineSignal
{
    /// <summary>
    /// Scene4 祭坛仪式特效控制器
    /// 由 Scene4_ceremony Timeline 的 Signal Track 驱动各阶段特效
    /// </summary>
    public class CeremonyEffectsController : MonoBehaviour
    {
        #region SerializedFields

        [Header("altar and beam")]
        [SerializeField] private Transform altarCenter;
        [SerializeField] private GameObject beamPrefab;
        [SerializeField] private float beamRiseDuration = 1.5f;
        [SerializeField] private float beamMaxHeight = 80f;
        [SerializeField] private float beamStartWidth = 0.3f;
        [SerializeField] private float beamEndWidth = 0.05f;

        [Header("boom vfx")]
        [SerializeField] private ParticleSystem explosionParticle;
        [SerializeField] private Transform explosionPoint;

        [Header("hoshi sora")]
        [Tooltip("场景中预置的动物物体（失活状态），每个一种动物")]
        [SerializeField] private GameObject[] animalObjects;
        [Tooltip("每个动物在星空中使用的 Shadow 材质")]
        [SerializeField] private Material animalShadowMaterial;
        [Tooltip("星空球壳半径")]
        [SerializeField] private float starfieldRadius = 60f;
        [Tooltip("星空最低高度")]
        [SerializeField] private float starMinHeight = 15f;
        [Tooltip("星空最高高度")]
        [SerializeField] private float starMaxHeight = 50f;
        [Tooltip("星星逐个出现的间隔（秒）")]
        [SerializeField] private float starfieldSpawnInterval = 0.1f;
        [Tooltip("每个动物的 SpiritSummonConfig（和 animalObjects 一一对应）")]
        [SerializeField] private StarlightCollect.SpiritSummonConfig[] spiritConfigs;

        [Header("Timeline")]
        [SerializeField] private PlayableDirector ceremonyDirector;

        [Header("dissolve")]
        [SerializeField] private DissolutionCenter dissolutionCenter;

        #endregion

        #region Private

        private GameObject beamInstance;
        private bool isPlaying;
        private Transform starfieldRoot;

        #endregion

        #region Timeline Signal Methods

        /// <summary>
        /// Phase 1: 祭坛中心汇聚白光，如流星般射向夜空
        /// </summary>
        public async void FireBeam()
        {
            if (beamInstance != null) return;
            if (altarCenter == null)
            {
                Debug.LogWarning("[CeremonyEffects] altarCenter 未设置");
                return;
            }

            beamInstance = beamPrefab != null
                ? Instantiate(beamPrefab, altarCenter.position, Quaternion.identity, altarCenter)
                : CreateBeamDefault();

            beamInstance.transform.localPosition = altarCenter.position;
            beamInstance.transform.localScale = new Vector3(beamStartWidth, 0.01f, beamStartWidth);

            await beamInstance.transform.DOScaleY(beamMaxHeight, beamRiseDuration)
                .SetEase(Ease.OutQuad)
                .AsyncWaitForCompletion();

            await beamInstance.transform.DOScaleX(beamEndWidth, 0.3f).SetEase(Ease.InQuad).AsyncWaitForCompletion();
            await beamInstance.transform.DOScaleZ(beamEndWidth, 0.3f).SetEase(Ease.InQuad).AsyncWaitForCompletion();
        }

        /// <summary>
        /// Phase 2: 光束在极高处炸开，如烟花般绽放
        /// </summary>
        public async void ExplodeBeam()
        {
            if (explosionParticle != null)
            {
                if (explosionPoint != null)
                    explosionParticle.transform.position = explosionPoint.position;
                else if (beamInstance != null)
                    explosionParticle.transform.position = beamInstance.transform.position + Vector3.up * beamMaxHeight;

                explosionParticle.Play();
            }

            if (beamInstance != null)
            {
                Material beamMat = beamInstance.GetComponent<Renderer>()?.material;
                if (beamMat != null)
                    await beamMat.DOFade(0f, 0.8f).AsyncWaitForCompletion();
                else
                    await UniTask.WaitForSeconds(0.8f);

                Destroy(beamInstance);
                beamInstance = null;
            }
        }

        /// <summary>
        /// Phase 3: 激活预置的动物剪影物体，随机放置到祭坛上空，绕祭坛旋转
        /// </summary>
        public async void SpawnStarfield()
        {
            if (animalObjects == null || animalObjects.Length == 0)
            {
                Debug.LogWarning("[CeremonyEffects] animalObjects 未设置");
                return;
            }

            // 创建星空根节点
            if (starfieldRoot == null)
            {
                starfieldRoot = new GameObject("StarfieldRoot").transform;
                starfieldRoot.position = altarCenter != null ? altarCenter.position : Vector3.zero;
            }

            Vector3 center = starfieldRoot.position;

            for (int i = 0; i < animalObjects.Length; i++)
            {
                GameObject obj = animalObjects[i];
                if (obj == null) continue;

                // 随机星空位置（上半球壳）
                Vector3 randomDir = Random.onUnitSphere;
                randomDir.y = Mathf.Abs(randomDir.y);
                randomDir.Normalize();

                float radius = Random.Range(starfieldRadius * 0.4f, starfieldRadius);
                Vector3 pos = center + randomDir * radius;
                pos.y = center.y + Random.Range(starMinHeight, starMaxHeight);

                // 激活并配置
                obj.transform.position = pos;
                obj.transform.rotation = Quaternion.identity;

                // 获取或添加 StarTwinkleInteraction
                var interaction = obj.GetComponent<StarTwinkleInteraction>();
                if (interaction == null)
                    interaction = obj.AddComponent<StarTwinkleInteraction>();

                // 分配 Shadow 材质和配置
                if (animalShadowMaterial != null)
                    interaction.shadowMaterial = animalShadowMaterial;

                if (spiritConfigs != null && i < spiritConfigs.Length && spiritConfigs[i] != null)
                    interaction.summonConfig = spiritConfigs[i];

                // 放入星空
                interaction.PlaceInSky(starfieldRoot, pos);

                // 添加闪烁
                var twinkle = obj.GetComponent<StarTwinkle>();
                if (twinkle == null)
                    twinkle = obj.AddComponent<StarTwinkle>();
                twinkle.delay = Random.Range(0f, 1.5f);
                twinkle.twinkleSpeed = Random.Range(0.5f, 1.5f);

                // 添加 XRGrabInteractable（如果没有）
                var grab = obj.GetComponent<XRGrabInteractable>();
                if (grab == null) grab = obj.AddComponent<XRGrabInteractable>();
                grab.throwOnDetach = false;
                grab.trackRotation = false;
                grab.trackPosition = false;

                // 添加 Rigidbody（如果没有）
                var rb = obj.GetComponent<Rigidbody>();
                if (rb == null) rb = obj.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                await UniTask.WaitForSeconds(starfieldSpawnInterval);
            }
        }

        /// <summary>
        /// Phase 4: 所有星星开始闪烁
        /// </summary>
        public void BeginTwinkle()
        {
            if (animalObjects == null) return;
            foreach (var obj in animalObjects)
            {
                if (obj == null || !obj.activeSelf) continue;
                var twinkle = obj.GetComponent<StarTwinkle>();
                if (twinkle != null)
                    twinkle.StartTwinkle();
            }
        }

        /// <summary>
        /// 停止所有特效
        /// </summary>
        public void StopAllEffects()
        {
            isPlaying = false;

            if (beamInstance != null)
            {
                beamInstance.transform.DOKill();
                Destroy(beamInstance);
                beamInstance = null;
            }

            if (explosionParticle != null)
                explosionParticle.Stop();

            if (animalObjects != null)
            {
                foreach (var obj in animalObjects)
                {
                    if (obj != null)
                        obj.transform.DOKill();
                }
            }
        }

        /// <summary>
        /// 清理星空（失活所有动物剪影）
        /// </summary>
        public void ClearStarfield()
        {
            if (animalObjects != null)
            {
                foreach (var obj in animalObjects)
                {
                    if (obj != null)
                        obj.SetActive(false);
                }
            }
        }

        #endregion

        #region Private Helpers

        private GameObject CreateBeamDefault()
        {
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "CeremonyBeam";

            Destroy(beam.GetComponent<Collider>());

            Material beamMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            beamMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.9f));
            beamMat.EnableKeyword("_EMISSION");
            beamMat.SetColor("_EmissionColor", new Color(1f, 1f, 0.95f) * 2f);
            beam.GetComponent<Renderer>().material = beamMat;

            return beam;
        }

        #endregion

        #region Lifecycle

        private void OnDestroy()
        {
            StopAllEffects();
            ClearStarfield();
        }

        #endregion
    }
}