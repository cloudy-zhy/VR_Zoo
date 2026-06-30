using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

namespace StarlightCollect
{
    /// <summary>
    /// 气泡显示在道具的左侧还是右侧（相对于玩家视线方向）。
    /// </summary>
    public enum BubbleSide
    {
        Left,
        Right
    }

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

        [Header("Text Bubble")]
        [Tooltip("显示“可拾取”文字的气泡 GameObject。若留空，可在编辑器中右键此组件选择 'Create Default Text Bubble' 自动生成。")]
        [SerializeField] private GameObject textBubble;

        [Tooltip("气泡出现在道具的左侧还是右侧（相对于玩家视线方向）。")]
        [SerializeField] private BubbleSide bubbleSide = BubbleSide.Right;

        [Tooltip("气泡在水平方向上离开道具的距离（米）。")]
        [SerializeField] private float bubbleHorizontalOffset = 0.4f;

        [Tooltip("气泡在垂直方向上离开道具的高度（米）。")]
        [SerializeField] private float bubbleHeight = 0.3f;

        [Header("Extra Binding")]
        [Tooltip("开启后，下方绑定的 GameObject 将随光柱同步显示/隐藏。")]
        [SerializeField] private bool enableExtraBinding = false;

        [Tooltip("随光柱同步显隐的 GameObject。不做任何位置/朝向处理，仅控制 Active。")]
        [SerializeField] private GameObject boundObject;

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

            bool shouldShow;

            if (isCurrentlyGrabbed)
            {
                shouldShow = false;
            }
            else
            {
                shouldShow = !(hidePermanentlyAfterFirstGrab && _hasBeenGrabbed);
            }

            guidanceEffect.SetActive(shouldShow);

            // 文字气泡与光柱同步显隐
            if (textBubble != null)
                textBubble.SetActive(shouldShow);

            if (enableExtraBinding && boundObject != null)
                boundObject.SetActive(shouldShow);
        }

        private void LateUpdate()
        {
            if (textBubble == null || !textBubble.activeSelf)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            float sign = (bubbleSide == BubbleSide.Right) ? 1f : -1f;

            textBubble.transform.position = transform.position
                + cam.transform.right * sign * bubbleHorizontalOffset
                + Vector3.up * bubbleHeight;

            textBubble.transform.forward = cam.transform.forward;
        }

        public void EnableExtraBinding()
        {
            enableExtraBinding = true;
            if (boundObject != null)
                boundObject.SetActive(guidanceEffect != null && guidanceEffect.activeSelf);
        }

        public void DisableExtraBinding()
        {
            enableExtraBinding = false;
            if (boundObject != null)
                boundObject.SetActive(false);
        }

        public bool IsGuidanceVisible()
        {
            return guidanceEffect != null && guidanceEffect.activeSelf;
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

        [ContextMenu("Create Default Text Bubble")]
        private void CreateDefaultTextBubble()
        {
            Transform existing = transform.Find("Default_Text_Bubble");
            GameObject canvasObj;

            if (existing != null)
            {
                canvasObj = existing.gameObject;
            }
            else
            {
                // 创建 World Space Canvas
                canvasObj = new GameObject("Default_Text_Bubble");
                canvasObj.transform.SetParent(transform, false);
                canvasObj.transform.localPosition = Vector3.zero;

                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 100;
                scaler.referencePixelsPerUnit = 100;

                canvasObj.AddComponent<GraphicRaycaster>();

                // 设置 Canvas 尺寸
                RectTransform canvasRt = canvasObj.GetComponent<RectTransform>();
                canvasRt.sizeDelta = new Vector2(2f, 1f);

                // 创建背景图
                GameObject bgObj = new GameObject("Bubble_BG");
                bgObj.transform.SetParent(canvasObj.transform, false);
                Image bgImage = bgObj.AddComponent<Image>();
                bgImage.color = new Color(0, 0, 0, 0.7f);

                RectTransform bgRt = bgObj.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;

                // 创建文字
                GameObject textObj = new GameObject("Bubble_Text");
                textObj.transform.SetParent(canvasObj.transform, false);
                TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
                tmp.text = "可拾取";
                tmp.fontSize = 24;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;

                RectTransform textRt = textObj.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(10, 5);
                textRt.offsetMax = new Vector2(-10, -5);
            }

            textBubble = canvasObj;
            UnityEditor.EditorUtility.SetDirty(gameObject);

            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }

            Debug.Log("Text Bubble created and assigned successfully. Please save the scene or prefab.", this);
        }
        #endif
    }
}
