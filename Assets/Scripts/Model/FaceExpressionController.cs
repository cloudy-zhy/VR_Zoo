using System.Collections.Generic;
using UnityEngine;

public class FaceExpressionController : MonoBehaviour
{
    [Header("基础设置 (Base Settings)")]
    public Renderer faceRenderer;
    public int materialIndex = 0;
    public string texturePropertyName = "_BaseMap";

    [Header("贴图网格设置 (Grid Settings)")]
    public int gridX = 3;               // 表情图有几列
    public int gridY = 3;               // 表情图有几行

    [Header("动画状态与表情映射 (Animation to Expression)")]
    public Animator animator;
    public List<AnimExpressionMap> expressionMappings;

    [Header("待机特殊配置 (Idle Random Settings)")]
    public string idleStateName = "Idle";
    public int defaultIdleExpression = 0;
    public int randomIdleExpression = 1;
    public float randomMinInterval = 2f;
    public float randomMaxInterval = 5f;
    public float randomDuration = 0.15f;

    [System.Serializable]
    public struct AnimExpressionMap
    {
        public string animStateName;
        public int expressionIndex;
    }

    private Material faceMaterial;
    private Vector2 uvStepSize; // UV 每次移动的固定步长
    private int currentAnimatorStateHash;
    private int currentExpressionIndex = -1;

    private bool isIdle = false;
    private float nextRandomTime = 0f;
    private float currentTimer = 0f;
    private bool isShowingRandomExpression = false;

    void Start()
    {
        if (faceRenderer == null) faceRenderer = GetComponentInChildren<Renderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // 实例化材质并获取引用
        faceMaterial = faceRenderer.materials[materialIndex];

        // 计算UV每次需要平移的固定距离 (例如 3x3 的图，每次平移 0.3333)
        uvStepSize = new Vector2(1f / gridX, 1f / gridY);

        // 【核心修改点】：因为你的UV在建模软件里已经缩放好了，所以这里不再调用 SetTextureScale 缩小贴图
        // 直接使用材质默认的 Tiling (1,1) 即可

        ResetIdleTimer();
    }

    void Update()
    {
        if (animator == null) return;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        if (stateInfo.shortNameHash != currentAnimatorStateHash)
        {
            currentAnimatorStateHash = stateInfo.shortNameHash;
            OnAnimatorStateChanged();
        }

        if (isIdle)
        {
            HandleIdleRandomExpression();
        }
    }

    private void OnAnimatorStateChanged()
    {
        bool stateFound = false;

        foreach (var map in expressionMappings)
        {
            if (currentAnimatorStateHash == Animator.StringToHash(map.animStateName))
            {
                if (map.animStateName == idleStateName)
                {
                    isIdle = true;
                    ResetIdleTimer();
                    SetExpression(defaultIdleExpression);
                }
                else
                {
                    isIdle = false;
                    SetExpression(map.expressionIndex);
                }
                stateFound = true;
                break;
            }
        }

        if (!stateFound && currentAnimatorStateHash != Animator.StringToHash(idleStateName))
        {
            isIdle = false;
            SetExpression(defaultIdleExpression);
        }
    }

    private void HandleIdleRandomExpression()
    {
        currentTimer += Time.deltaTime;

        if (!isShowingRandomExpression)
        {
            if (currentTimer >= nextRandomTime)
            {
                SetExpression(randomIdleExpression);
                isShowingRandomExpression = true;
                currentTimer = 0f;
            }
        }
        else
        {
            if (currentTimer >= randomDuration)
            {
                SetExpression(defaultIdleExpression);
                isShowingRandomExpression = false;
                ResetIdleTimer();
            }
        }
    }

    private void ResetIdleTimer()
    {
        currentTimer = 0f;
        nextRandomTime = Random.Range(randomMinInterval, randomMaxInterval);
    }

    // 核心算法：通过表情序号，移动固定距离的 UV
    private void SetExpression(int index)
    {
        if (currentExpressionIndex == index) return;

        // 计算当前序号对应要移动几个格子
        int col = index % gridX;
        int row = index / gridX;

        // 【核心修改点】：因为起始UV就在左下角，而Unity的UV原点(0,0)也是左下角
        // 所以直接往右(U)和往上(V)加上固定距离即可，不需要任何复杂的翻转了！
        float uOffset = col * uvStepSize.x;
        float vOffset = row * uvStepSize.y;

        // 改变 Offset，在 Shader 层面就是把 UV 挪动了这么多距离
        faceMaterial.SetTextureOffset(texturePropertyName, new Vector2(uOffset, vOffset));

        currentExpressionIndex = index;
    }
}