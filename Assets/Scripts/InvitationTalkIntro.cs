using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class InvitationPlayAnim : MonoBehaviour
{
    [Header("邀请函下落总时长")]
    public float fallTime = 2.7f;
    [Header("自转一圈耗时（秒）")]
    public float rotateCycle = 3.2f;
    [Header("落地后上下浮动幅度")]
    public float floatRange = 14f;
    [Header("上下浮动速度")]
    public float floatSpeed = 1.1f;

    [Header("UI锚点偏移坐标（适配WorldSpace画布）")]
    public Vector2 startAnchoredPos = new Vector2(0, 500);
    public Vector2 endAnchoredPos = new Vector2(0, -320);

    [Header("语音收尾淡出设置")]
    [Tooltip("语音剩余多少秒开始淡出")]
    public float fadeStartBeforeEnd = 1.2f;
    [Tooltip("淡出总耗时")]
    public float fadeDuration = 1.0f;

    private RectTransform rect;
    private AudioSource voiceAudio;
    private Image inviteImage;

    // 由Timeline激活时才触发动画，不再Start自动跑
    void OnEnable()
    {
        rect = GetComponent<RectTransform>();
        voiceAudio = GetComponent<AudioSource>();
        inviteImage = GetComponent<Image>();

        // 初始化完全不透明、归位起点
        Color initCol = inviteImage.color;
        initCol.a = 1f;
        inviteImage.color = initCol;

        rect.anchoredPosition = startAnchoredPos;
        Debug.Log("初始锚点位置：" + rect.anchoredPosition);

        StopAllCoroutines();
        StartCoroutine(PlayFallAnim());
    }

    IEnumerator PlayFallAnim()
    {
        float timer = 0f;
        while (timer < fallTime)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / fallTime);

            rect.anchoredPosition = Vector2.Lerp(startAnchoredPos, endAnchoredPos, progress);
            rect.Rotate(0, 0, (360f / rotateCycle) * Time.deltaTime);

            Debug.Log($"下落进度{progress:F2} 当前锚点Y：{rect.anchoredPosition.y}");
            yield return null;
        }

        // 落地摆正Z旋转，纸张不歪斜
        Vector3 eulerAng = rect.localEulerAngles;
        eulerAng.z = 0f;
        rect.localEulerAngles = eulerAng;

        if (voiceAudio != null && voiceAudio.clip != null)
        {
            voiceAudio.Play();
            Debug.Log("邀请函落地摆正，开始小翼龙朗读");
            yield return StartCoroutine(VoiceAndFadeLogic());
        }
        else
        {
            Debug.LogWarning("AudioSource 没有赋值音频Clip，无法播放语音");
            StartCoroutine(FloatLoopAnim());
        }
    }

    // 语音播放监听 + 末尾渐隐逻辑
    IEnumerator VoiceAndFadeLogic()
    {
        AudioClip clip = voiceAudio.clip;
        float totalTime = clip.length;

        // 正常浮动到即将结束
        while (voiceAudio.isPlaying && (totalTime - voiceAudio.time) > fadeStartBeforeEnd)
        {
            yield return MoveAnchoredY(rect.anchoredPosition.y, rect.anchoredPosition.y + floatRange, floatSpeed / 2);
            yield return MoveAnchoredY(rect.anchoredPosition.y + floatRange, rect.anchoredPosition.y, floatSpeed / 2);
        }

        // 透明度慢慢淡出
        Debug.Log("语音即将结束，开始邀请函淡出");
        float fadeTimer = 0f;
        Color curColor = inviteImage.color;
        float startAlpha = curColor.a;

        while (fadeTimer < fadeDuration)
        {
            fadeTimer += Time.deltaTime;
            float fadeProgress = fadeTimer / fadeDuration;
            curColor.a = Mathf.Lerp(startAlpha, 0f, fadeProgress);
            inviteImage.color = curColor;
            yield return null;
        }

        curColor.a = 0f;
        inviteImage.color = curColor;
        Debug.Log("邀请函透明度淡出完成，物体隐藏由Timeline控制");
        // ========= 删除了 gameObject.SetActive(false) =========
    }

    IEnumerator FloatLoopAnim()
    {
        float baseY = rect.anchoredPosition.y;
        while (true)
        {
            yield return MoveAnchoredY(baseY, baseY + floatRange, floatSpeed / 2);
            yield return MoveAnchoredY(baseY + floatRange, baseY, floatSpeed / 2);
        }
    }

    IEnumerator MoveAnchoredY(float fromY, float toY, float duration)
    {
        float t = 0;
        Vector2 curPos = rect.anchoredPosition;
        while (t < duration)
        {
            t += Time.deltaTime;
            curPos.y = Mathf.Lerp(fromY, toY, t / duration);
            rect.anchoredPosition = curPos;
            yield return null;
        }
    }
}