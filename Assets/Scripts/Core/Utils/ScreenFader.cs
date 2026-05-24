using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Utils
{
    /// <summary>
    /// 基于主相机的全屏淡入淡出遮罩。
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        #region Inspector

        [Header("淡入淡出设置")]
        [Tooltip("淡入淡出持续时间。")]
        [SerializeField] private float gradientTime = 5.0f;

        [Tooltip("遮罩基础颜色。")]
        [SerializeField] private Color fadeColor = Color.black;

        [Tooltip("遮罩材质渲染队列。")]
        [SerializeField] private int renderQueue = 4000;

        #endregion

        #region Fields

        private const int MeshSegments = 5;
        private const float MeshRadius = 0.7f;

        private GameObject fadeGameObject;
        private MeshRenderer gradientMeshRenderer;
        private MeshFilter gradientMeshFilter;
        private Material gradientMaterial;
        private Coroutine fadeCoroutine;
        private bool isGradient;
        private float currentAlpha;
        private float nowFadeAlpha;
        private List<Vector3> verts;
        private List<int> indices;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            fadeGameObject = Camera.main.gameObject;
            CreateFadeMesh();
            SetCurrentAlpha(0f);
        }

        private void OnDestroy()
        {
            DestroyGradientMesh();
        }

        #endregion

        #region Public API

        /// <summary>
        /// 从黑屏淡入到透明。
        /// </summary>
        [ContextMenu("Fade In")]
        public void FadeIn()
        {
            StopFadeRoutine();
            fadeCoroutine = StartCoroutine(ScreenFadeRoutine(1f, 0f));
        }

        /// <summary>
        /// 从透明淡出到黑屏。
        /// </summary>
        [ContextMenu("Fade Out")]
        public void FadeOut()
        {
            StopFadeRoutine();
            fadeCoroutine = StartCoroutine(ScreenFadeRoutine(0f, 1f));
        }

        /// <summary>
        /// 立即设置当前屏幕遮罩透明度。
        /// </summary>
        public void SetCurrentAlpha(float alpha)
        {
            StopFadeRoutine();
            currentAlpha = Mathf.Clamp01(alpha);
            nowFadeAlpha = 0f;
            SetAlpha(currentAlpha);
        }

        #endregion

        #region Fade

        private void StopFadeRoutine()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
        }

        private IEnumerator ScreenFadeRoutine(float from, float to)
        {
            float elapsedTime = 0f;
            from = Mathf.Clamp01(from);
            to = Mathf.Clamp01(to);

            while (elapsedTime < gradientTime)
            {
                elapsedTime += Time.deltaTime;
                nowFadeAlpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsedTime / gradientTime));
                SetAlpha(nowFadeAlpha);
                yield return null;
            }

            nowFadeAlpha = to;
            currentAlpha = to;
            SetAlpha(to);
            fadeCoroutine = null;
        }

        private void SetAlpha(float alpha)
        {
            Color color = fadeColor;
            color.a = Mathf.Clamp01(alpha);
            isGradient = color.a > 0f;

            gradientMaterial.color = color;
            gradientMaterial.renderQueue = renderQueue;
            gradientMeshRenderer.enabled = isGradient;
        }

        #endregion

        #region Mesh

        private void CreateFadeMesh()
        {
            verts = new List<Vector3>();
            indices = new List<int>();
            gradientMaterial = new Material(Shader.Find("PXR_SDK/PXR_Fade"));
            if (!fadeGameObject.TryGetComponent<MeshFilter>(out gradientMeshFilter))
                gradientMeshFilter = fadeGameObject.AddComponent<MeshFilter>();
            if (!fadeGameObject.TryGetComponent<MeshRenderer>(out gradientMeshRenderer))
                gradientMeshRenderer = fadeGameObject.AddComponent<MeshRenderer>();

            CreateModel();
        }

        private void CreateModel()
        {
            for (float i = -MeshSegments / 2f; i <= MeshSegments / 2f; i++)
            {
                for (float j = -MeshSegments / 2f; j <= MeshSegments / 2f; j++)
                {
                    verts.Add(new Vector3(i, j, -MeshSegments / 2f));
                }
            }

            for (float i = -MeshSegments / 2f; i <= MeshSegments / 2f; i++)
            {
                for (float j = -MeshSegments / 2f; j <= MeshSegments / 2f; j++)
                {
                    verts.Add(new Vector3(MeshSegments / 2f, j, i));
                }
            }

            for (float i = -MeshSegments / 2f; i <= MeshSegments / 2f; i++)
            {
                for (float j = -MeshSegments / 2f; j <= MeshSegments / 2f; j++)
                {
                    verts.Add(new Vector3(i, MeshSegments / 2f, j));
                }
            }

            for (float i = -MeshSegments / 2f; i <= MeshSegments / 2f; i++)
            {
                for (float j = -MeshSegments / 2f; j <= MeshSegments / 2f; j++)
                {
                    verts.Add(new Vector3(-MeshSegments / 2f, j, i));
                }
            }

            for (float i = -MeshSegments / 2f; i <= MeshSegments / 2f; i++)
            {
                for (float j = -MeshSegments / 2f; j <= MeshSegments / 2f; j++)
                {
                    verts.Add(new Vector3(i, j, MeshSegments / 2f));
                }
            }

            for (float i = -MeshSegments / 2f; i <= MeshSegments / 2f; i++)
            {
                for (float j = -MeshSegments / 2f; j <= MeshSegments / 2f; j++)
                {
                    verts.Add(new Vector3(i, -MeshSegments / 2f, j));
                }
            }

            for (int i = 0; i < verts.Count; i++)
            {
                verts[i] = verts[i].normalized * MeshRadius;
            }

            CreateMakePos(0);
            CreateMakePos(1);
            CreateMakePos(2);
            OtherMakePos(3);
            OtherMakePos(4);
            OtherMakePos(5);

            Mesh mesh = new Mesh
            {
                name = "Screen Fade Mesh",
                vertices = verts.ToArray(),
                triangles = indices.ToArray()
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            Vector3[] normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = -normals[i];
            }

            mesh.normals = normals;

            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                (triangles[i], triangles[i + 2]) = (triangles[i + 2], triangles[i]);
            }

            mesh.triangles = triangles;
            gradientMeshFilter.sharedMesh = mesh;
            gradientMeshRenderer.sharedMaterial = gradientMaterial;
        }

        private void CreateMakePos(int num)
        {
            for (int i = 0; i < MeshSegments; i++)
            {
                for (int j = 0; j < MeshSegments; j++)
                {
                    int index = j * (MeshSegments + 1) + (MeshSegments + 1) * (MeshSegments + 1) * num + i;
                    int up = (j + 1) * (MeshSegments + 1) + (MeshSegments + 1) * (MeshSegments + 1) * num + i;
                    indices.AddRange(new[] { index, index + 1, up + 1 });
                    indices.AddRange(new[] { index, up + 1, up });
                }
            }
        }

        private void OtherMakePos(int num)
        {
            for (int i = 0; i < MeshSegments + 1; i++)
            {
                for (int j = 0; j < MeshSegments + 1; j++)
                {
                    if (i == MeshSegments || j == MeshSegments)
                    {
                        continue;
                    }

                    int index = j * (MeshSegments + 1) + (MeshSegments + 1) * (MeshSegments + 1) * num + i;
                    int up = (j + 1) * (MeshSegments + 1) + (MeshSegments + 1) * (MeshSegments + 1) * num + i;
                    indices.AddRange(new[] { index, up + 1, index + 1 });
                    indices.AddRange(new[] { index, up, up + 1 });
                }
            }
        }

        private void DestroyGradientMesh()
        {
            if (gradientMeshRenderer != null)
                Destroy(gradientMeshRenderer);

            if (gradientMaterial != null)
            {
                if (gradientMeshFilter.sharedMesh != null)
                    Destroy(gradientMeshFilter.sharedMesh);
                Destroy(gradientMaterial);
            }

            if (gradientMeshFilter != null)
                Destroy(gradientMeshFilter);
        }

        #endregion
    }
}
