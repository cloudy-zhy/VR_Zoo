using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BillboardProgressBar : MonoBehaviour
{
    [Header("进度条设置")]
    [Tooltip("完成所需时间（秒）")]
    public float duration = 10f;          // ← 只需修改这个数值

    [Header("UI 引用")]
    public RectTransform fillRect;        // Fill 图片的 RectTransform
    public RectTransform backgroundRect;  // Background 的 RectTransform
    public TextMeshProUGUI label;         // 百分比文字

    [Header("公告板朝向")]
    public bool faceCamera = true;        // 是否始终朝向摄像机

    private float elapsed = 0f;
    private bool isRunning = false;

    void Start()
    {
        StartProgress();
    }

    void Update()
    {
        // 公告板朝向摄像机
        if (faceCamera && Camera.main != null)
        {
            transform.LookAt(Camera.main.transform);
            transform.Rotate(0, 180, 0); // 修正朝向
        }

        // 进度更新
        if (isRunning)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            UpdateBar(progress);

            if (progress >= 1f)
            {
                isRunning = false;
                if (label) label.text = "完成！";
            }
        }
    }

    public void StartProgress()
    {
        elapsed = 0f;
        isRunning = true;
    }

    public void SetDuration(float newDuration)
    {
        duration = newDuration;
        StartProgress();
    }

    void UpdateBar(float progress)
    {
        // 根据进度设置 Fill 的宽度
        float maxWidth = backgroundRect.rect.width;
        fillRect.sizeDelta = new Vector2(maxWidth * progress, fillRect.sizeDelta.y);

        // 更新文字
        if (label) label.text = $"{Mathf.RoundToInt(progress * 100)}%";

        // 颜色渐变（绿→黄→红 可选）
        //Image fillImage = fillRect.GetComponent<Image>();
        //if (fillImage)
        //    fillImage.color = Color.Lerp(Color.green, Color.red, progress);
    }
}