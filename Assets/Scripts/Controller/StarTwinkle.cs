using UnityEngine;
/// <summary>
/// 星星闪烁组件
/// </summary>
public class StarTwinkle : MonoBehaviour
{
    public float delay = 0f;
    public float twinkleSpeed = 1f;
    public float minAlpha = 0.2f;
    public float maxAlpha = 1f;

    private Material mat;
    private float timer;
    private bool started;
    private Color baseColor;

    void Awake()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
            mat = rend.material;
    }

    void Start()
    {
        if (mat != null)
            baseColor = mat.GetColor("_BaseColor");
    }

    public void StartTwinkle()
    {
        started = true;
        timer = delay;
    }

    void Update()
    {
        if (!started || mat == null) return;

        timer += Time.deltaTime * twinkleSpeed;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(timer) + 1f) * 0.5f);
            
        Color c = baseColor;
        c.a = alpha;
        mat.SetColor("_BaseColor", c);
    }

    private void OnDestroy()
    {
        if (mat != null)
            Destroy(mat);
    }
}