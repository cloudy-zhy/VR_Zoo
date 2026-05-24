using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Utils
{
    /// <summary>
    /// 基于当前 Renderer 的全屏淡入淡出遮罩。
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        #region Inspector

        [Header("淡入淡出设置")]
        [Tooltip("是否开始时淡入。")]
        [SerializeField] private bool fadeInOnStart = false;
        [Tooltip("淡入淡出持续时间。")]
        [SerializeField] private float fadeDuration = 5.0f;

        [Tooltip("遮罩基础颜色。")]
        [SerializeField] private Color fadeColor = Color.black;

        [Tooltip("淡入淡出曲线。")]
        public AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        #endregion

        #region Fields

        private Renderer m_rend;
        private Coroutine m_corou;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            m_rend = GetComponent<Renderer>();
            m_rend.enabled = false;

            if (fadeInOnStart)
                FadeIn();
        }

        #endregion

        #region Public API

        /// <summary>
        /// 从黑屏淡入到透明。
        /// </summary>
        [ContextMenu("Fade In")]
        public void FadeIn()
        {
            Fade(1f, 0f);
        }

        /// <summary>
        /// 从透明淡出到黑屏。
        /// </summary>
        [ContextMenu("Fade Out")]
        public void FadeOut()
        {
            Fade(0f, 1f);
        }

        /// <summary>
        /// 从指定透明度淡入淡出到目标透明度。
        /// </summary>
        public void Fade(float from, float to)
        {
            if (m_corou != null)
            {
                StopCoroutine(m_corou);
            }

            m_corou = StartCoroutine(FadeRoutine(from, to));
        }

        /// <summary>
        /// 按淡入淡出曲线执行屏幕遮罩透明度变化。
        /// </summary>
        public IEnumerator FadeRoutine(float from, float to)
        {
            m_rend.enabled = true;

            float timer = 0f;
            while (timer <= fadeDuration)
            {
                Color color = fadeColor;
                color.a = Mathf.Lerp(from, to, fadeCurve.Evaluate(timer / fadeDuration));

                m_rend.material.color = color;

                timer += Time.deltaTime;
                yield return null;
            }

            Color finalColor = fadeColor;
            finalColor.a = to;
            m_rend.material.color = finalColor;

            m_rend.enabled = to > 0f;
            m_corou = null;
        }

        #endregion

        #region Editor
#if UNITY_EDITOR
        
        private const int renderQueue = 4000;
        private const int MeshSegments = 5;
        private const float MeshRadius = 0.7f;
        private const string FaderAssetFolder = "Assets/Prefabs/ScreenFader";
        private const string FaderMeshPath = FaderAssetFolder + "/Msh_Fader.asset";
        private const string FaderMaterialPath = FaderAssetFolder + "/Mat_Fader.mat";

        [ContextMenu("Create/Overwrite Msh_Fader")]
        private void CreateOrOverwriteFaderMesh()
        {
            List<Vector3> verts = new();
            List<int> indices = new();

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

            Mesh mesh = new()
            {
                name = "Msh_Fader",
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

            if (!AssetDatabase.IsValidFolder(FaderAssetFolder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "ScreenFader");
            }

            AssetDatabase.DeleteAsset(FaderMeshPath);
            AssetDatabase.CreateAsset(mesh, FaderMeshPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            void CreateMakePos(int num)
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

            void OtherMakePos(int num)
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
        }

        [ContextMenu("Create/Overwrite Mat_Fader")]
        private void CreateOrOverwriteFaderMaterial()
        {
            if (!AssetDatabase.IsValidFolder(FaderAssetFolder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "ScreenFader");
            }

            Shader shader = Shader.Find("PXR_SDK/PXR_Fade");
            if (shader == null)
            {
                Debug.LogError("未找到 Shader：PXR_SDK/PXR_Fade。", this);
                return;
            }

            Material material = new(shader)
            {
                name = "Mat_Fader",
                color = fadeColor,
                renderQueue = renderQueue
            };

            AssetDatabase.DeleteAsset(FaderMaterialPath);
            AssetDatabase.CreateAsset(material, FaderMaterialPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

#endif
        #endregion
    }
}
