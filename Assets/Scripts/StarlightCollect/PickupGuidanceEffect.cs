using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace StarlightCollect
{
    /// <summary>
    /// 拾取引导效果组件。
    /// 挂载在包含 XRGrabInteractable 的物体上，未被拾取时显示光柱，拾取后关闭光柱。
    /// 支持在 Inspector 中通过 ContextMenu 一键生成简易圆柱体光柱。
    /// </summary>
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class PickupGuidanceEffect : MonoBehaviour
    {
        [Header("Guidance Settings")]
        [Tooltip("需要作为光柱/强调效果的 GameObject。若留空，可在编辑器中右键此组件选择 'Create Default Beam' 自动生成。")]
        [SerializeField] private GameObject guidanceEffect;

        [Tooltip("是否在玩家第一次拾取后永久隐藏此引导效果。")]
        [SerializeField] private bool hidePermanentlyAfterFirstGrab = true;



        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;
        private bool _hasBeenGrabbed = false;

        private void Start()
        {
            _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.AddListener(OnSelectEntered);
                _grabInteractable.selectExited.AddListener(OnSelectExited);
            }

            // 初始化显隐状态
            UpdateGuidanceState(false);
        }

        private void OnDestroy()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
                _grabInteractable.selectExited.RemoveListener(OnSelectExited);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            _hasBeenGrabbed = true;
            UpdateGuidanceState(true);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            UpdateGuidanceState(false);
        }

        private void UpdateGuidanceState(bool isCurrentlyGrabbed)
        {
            if (guidanceEffect == null) 
                return;

            if (isCurrentlyGrabbed)
            {
                guidanceEffect.SetActive(false);
            }
            else
            {
                if (hidePermanentlyAfterFirstGrab && _hasBeenGrabbed)
                {
                    guidanceEffect.SetActive(false);
                }
                else
                {
                    guidanceEffect.SetActive(true);
                }
            }
        }

        #if UNITY_EDITOR
        [ContextMenu("Create Default Beam")]
        private void CreateDefaultBeam()
        {
            // 查找是否已存在默认生成的子物体，防止重复创建
            Transform existing = transform.Find("Default_Guidance_Beam");
            GameObject beamObj;
            
            if (existing != null)
            {
                beamObj = existing.gameObject;
            }
            else
            {
                // 创建一个圆柱体作为简易光柱
                beamObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beamObj.name = "Default_Guidance_Beam";
                beamObj.transform.SetParent(transform, false);
            }

            // 移除物理碰撞组件，防止阻挡抓取射线或产生物理碰撞
            Collider col = beamObj.GetComponent<Collider>();
            if (col != null)
            {
                DestroyImmediate(col);
            }

            // 调整尺寸和位置
            // 在双向拉伸 Shader 下，圆柱体自身网格大小保持 (1,1,1)，实际粗细和高度完全由材质/Shader控制
            beamObj.transform.localScale = Vector3.one;
            
            // 本地坐标 Y 设置为 0，使圆柱体中心与物品中心完美对齐
            beamObj.transform.localPosition = Vector3.zero;

            // 应用双向拉伸材质
            Renderer rendererComponent = beamObj.GetComponent<Renderer>();
            if (rendererComponent != null)
            {
                Material mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Shader/InfiniteBeamMaterial.mat");
                if (mat != null)
                {
                    rendererComponent.sharedMaterial = mat;
                }
                else
                {
                    Shader customShader = Shader.Find("Custom/InfiniteBeamShader");
                    if (customShader != null)
                    {
                        rendererComponent.sharedMaterial = new Material(customShader);
                    }
                }
            }

            // 自动关联到槽位
            guidanceEffect = beamObj;

            // 标记已脏，确保修改可以被保存
            UnityEditor.EditorUtility.SetDirty(gameObject);
            
            // 如果是在场景中修改，标记场景已脏
            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }

            Debug.Log("Guidance Beam created and assigned successfully. Please save the scene or prefab.", this);
        }
        #endif
    }
}
