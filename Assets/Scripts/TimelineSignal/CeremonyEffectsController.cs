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
        [SerializeField] private GameObject starfieldRoot;
        [SerializeField] private GameObject starPrefab;
        [SerializeField] private int starCount = 80;
        [SerializeField] private float starfieldRadius = 60f;
        [SerializeField] private float starMinHeight = 15f;
        [SerializeField] private float starMaxHeight = 50f;
        [SerializeField] private float starfieldSpawnDuration = 3f;
        [SerializeField] private Texture2D[] animalSilhouetteTextures;

        [Header("Timeline")]
        [SerializeField] private PlayableDirector ceremonyDirector;

        [Header("dissolve")]
        [SerializeField] private DissolutionCenter dissolutionCenter;

        #endregion

        #region Private

        private GameObject beamInstance;
        private List<GameObject> starInstances = new List<GameObject>();
        private bool isPlaying;

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

            // 实例化光束
            beamInstance = beamPrefab != null 
                ? Instantiate(beamPrefab, altarCenter.position, Quaternion.identity, altarCenter)
                : CreateBeamDefault();

            beamInstance.transform.localPosition = altarCenter.position;
            beamInstance.transform.localScale = new Vector3(beamStartWidth, 0.01f, beamStartWidth);

            // 光束冲天
            await beamInstance.transform.DOScaleY(beamMaxHeight, beamRiseDuration)
                .SetEase(Ease.OutQuad)
                .AsyncWaitForCompletion();

            // 光束尖端收窄
            await beamInstance.transform.DOScaleX(beamEndWidth, 0.3f).SetEase(Ease.InQuad).AsyncWaitForCompletion();
            await beamInstance.transform.DOScaleZ(beamEndWidth, 0.3f).SetEase(Ease.InQuad).AsyncWaitForCompletion();
        }

        /// <summary>
        /// Phase 2: 光束在极高处炸开，如烟花般绽放
        /// </summary>
        public async void ExplodeBeam()
        {
            // 播放爆炸粒子
            if (explosionParticle != null)
            {
                if (explosionPoint != null)
                    explosionParticle.transform.position = explosionPoint.position;
                else if (beamInstance != null)
                    explosionParticle.transform.position = beamInstance.transform.position + Vector3.up * beamMaxHeight;

                explosionParticle.Play();
            }

            // 光束消散
            if (beamInstance != null)
            {
                Material beamMat = beamInstance.GetComponent<Renderer>()?.material;
                if (beamMat != null)
                {
                    await beamMat.DOFade(0f, 0.8f).AsyncWaitForCompletion();
                }
                else
                {
                    await UniTask.WaitForSeconds(0.8f);
                }
                Destroy(beamInstance);
                beamInstance = null;
            }
        }

        /// <summary>
        /// Phase 3: 黑夜散满璀璨星河，星光是不同动物的剪影
        /// </summary>
        public async void SpawnStarfield()
        {
            if (starfieldRoot == null)
            {
                starfieldRoot = new GameObject("StarfieldRoot");
                starfieldRoot.transform.position = altarCenter != null ? altarCenter.position : Vector3.zero;
            }

            starfieldRoot.SetActive(true);

            Vector3 center = starfieldRoot.transform.position;

            for (int i = 0; i < starCount; i++)
            {
                SpawnStar(center, i);
                // 分批生成，避免卡顿
                if (i % 10 == 0)
                    await UniTask.Yield();
            }

            // 总持续时间
            float elapsed = 0f;
            while (elapsed < starfieldSpawnDuration)
            {
                elapsed += Time.deltaTime;
                await UniTask.Yield();
            }
        }

        /// <summary>
        /// Phase 4: 星星开始闪烁
        /// </summary>
        public void BeginTwinkle()
        {
            foreach (var star in starInstances)
            {
                if (star == null) continue;
                StarTwinkle starTwinkle = star.GetComponent<StarTwinkle>();
                if (starTwinkle != null)
                    starTwinkle.StartTwinkle();
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

            foreach (var star in starInstances)
            {
                if (star != null)
                    star.transform.DOKill();
            }
        }

        /// <summary>
        /// 清理星空
        /// </summary>
        public void ClearStarfield()
        {
            foreach (var star in starInstances)
            {
                if (star != null)
                    Destroy(star);
            }
            starInstances.Clear();
        }

        #endregion

        #region Private Helpers

        private GameObject CreateBeamDefault()
        {
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "CeremonyBeam";

            // 移除碰撞体
            Destroy(beam.GetComponent<Collider>());

            // 创建发光材质
            Material beamMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            beamMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.9f));
            beamMat.EnableKeyword("_EMISSION");
            beamMat.SetColor("_EmissionColor", new Color(1f, 1f, 0.95f) * 2f);
            beam.GetComponent<Renderer>().material = beamMat;

            return beam;
        }

        private void SpawnStar(Vector3 center, int index)
        {
            // 球壳上随机分布
            Vector3 randomDir = Random.onUnitSphere;
            randomDir.y = Mathf.Abs(randomDir.y); // 上半球
            randomDir.Normalize();

            float radius = Random.Range(starfieldRadius * 0.3f, starfieldRadius);
            Vector3 pos = center + randomDir * radius;
            pos.y = center.y + Random.Range(starMinHeight, starMaxHeight);

            GameObject star;
            if (starPrefab != null)
            {
                star = Instantiate(starPrefab, pos, Quaternion.identity, starfieldRoot.transform);
            }
            else
            {
                star = CreateStarDefault(pos);
            }

            // 随机大小
            float scale = Random.Range(0.3f, 1.5f);
            star.transform.localScale = Vector3.one * scale;

            // 面向下方（祭坛方向）
            star.transform.LookAt(center);
            star.transform.Rotate(90f, 0f, 0f); // 让平面面向下方

            // 随机动物剪影
            if (animalSilhouetteTextures != null && animalSilhouetteTextures.Length > 0)
            {
                Texture2D tex = animalSilhouetteTextures[Random.Range(0, animalSilhouetteTextures.Length)];
                Renderer rend = star.GetComponent<Renderer>();
                if (rend != null && rend.material != null)
                {
                    rend.material.SetTexture("_BaseMap", tex);
                    rend.material.SetTexture("_MainTex", tex);
                }
            }

            // 添加闪烁组件
            StarTwinkle twinkle = star.GetComponent<StarTwinkle>();
            if (twinkle == null)
                twinkle = star.AddComponent<StarTwinkle>();
            twinkle.delay = Random.Range(0f, 2f);
            twinkle.twinkleSpeed = Random.Range(0.5f, 2f);

            starInstances.Add(star);
        }

        private GameObject CreateStarDefault(Vector3 position)
        {
            GameObject star = GameObject.CreatePrimitive(PrimitiveType.Quad);
            star.name = "CeremonyStar";
            star.transform.position = position;
            star.transform.SetParent(starfieldRoot != null ? starfieldRoot.transform : null);

            Destroy(star.GetComponent<Collider>());

            Material starMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            starMat.SetColor("_BaseColor", new Color(1f, 1f, 0.9f, 0.8f));
            starMat.EnableKeyword("_EMISSION");
            starMat.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.8f) * 1.5f);
            starMat.SetFloat("_Blend", 0f);
            star.GetComponent<Renderer>().material = starMat;

            return star;
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
