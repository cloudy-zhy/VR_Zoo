using UnityEngine;

/// <summary>
/// 挂载到使用 TextureFlow/TransparentFlowBlue shader 的物体上。
/// 通过 MaterialPropertyBlock 驱动 UV 流动，避免创建材质实例。
/// 
/// 也可以不挂脚本 —— shader 自带 _Time.y 驱动。
/// 此脚本提供额外的运行时控制、速度渐变、Pause/Resume。
/// </summary>
public class TextureFlowController : MonoBehaviour
{
    [Header("Flow Speed")]
    [Tooltip("U 方向流速")]
    public float flowSpeedX = 0.5f;
    [Tooltip("V 方向流速")]
    public float flowSpeedY = 0f;

    [Header("Options")]
    [Tooltip("是否使用 MaterialPropertyBlock（不影响共享材质）")]
    public bool usePropertyBlock = true;

    private Renderer rend;
    private MaterialPropertyBlock mpb;
    private Material instancedMat;
    private static readonly int FlowSpeedXId = Shader.PropertyToID("_FlowSpeedX");
    private static readonly int FlowSpeedYId = Shader.PropertyToID("_FlowSpeedY");

    void Awake()
    {
        rend = GetComponent<Renderer>();
        if (usePropertyBlock)
            mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        ApplyFlowSpeed();
    }

    void Update()
    {
        // 持续应用流速（支持运行时在 Inspector 调整）
        ApplyFlowSpeed();
    }

    void ApplyFlowSpeed()
    {
        if (rend == null) return;

        if (usePropertyBlock && mpb != null)
        {
            rend.GetPropertyBlock(mpb);
            mpb.SetFloat(FlowSpeedXId, flowSpeedX);
            mpb.SetFloat(FlowSpeedYId, flowSpeedY);
            rend.SetPropertyBlock(mpb);
        }
        else
        {
            if (instancedMat == null)
                instancedMat = rend.material;
            instancedMat.SetFloat(FlowSpeedXId, flowSpeedX);
            instancedMat.SetFloat(FlowSpeedYId, flowSpeedY);
        }
    }

    /// <summary>
    /// 暂停流动
    /// </summary>
    public void Pause()
    {
        flowSpeedX = 0f;
        flowSpeedY = 0f;
    }

    /// <summary>
    /// 恢复流动
    /// </summary>
    public void Resume(float sx, float sy)
    {
        flowSpeedX = sx;
        flowSpeedY = sy;
    }

    void OnDestroy()
    {
        if (instancedMat != null)
            Destroy(instancedMat);
    }
}