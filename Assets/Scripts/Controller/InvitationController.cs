using Core.Event;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Manager;
using UnityEngine.Playables;

public class InvitationController : MonoBehaviour
{
    [Header("开场Timeline")]
    [SerializeField] private PlayableDirector director;
    
    [Header("下落动画设置")]
    [SerializeField] private float fallTime = 2.7f;
    [SerializeField] private float rotateCycle = 3.2f;
    [SerializeField] private Vector2 startAnchoredPos = new Vector2(0, 500);
    [SerializeField] private Vector2 endAnchoredPos = new Vector2(0, -320);

    [Header("浮动动画设置")]
    [SerializeField] private float floatRange = 14f;
    [SerializeField] private float floatSpeed = 1.1f;

    [Header("语音淡出设置")]
    [SerializeField] private float fadeStartBeforeEnd = 1.2f;
    [SerializeField] private float fadeDuration = 1.0f;

    private RectTransform m_rectTransform;
    private Image m_inviteImage;
    private AudioSource m_voiceAudio;
    private GameObject m_inviteGameObject;
    
    private Tween m_floatTween;

    private void Awake()
    {
        m_inviteGameObject = transform.GetChild(0).gameObject;
        m_rectTransform = m_inviteGameObject.GetComponent<RectTransform>();
        m_inviteImage = m_inviteGameObject.GetComponent<Image>();
        m_voiceAudio = m_inviteGameObject.GetComponent<AudioSource>();
        m_inviteGameObject.SetActive(false);
        GameManager.Event.Register("MenuBtn.StartGame", PlayIntro);
    }

    private void OnDestroy()
    {
        GameManager.Event.Unregister("MenuBtn.StartGame", PlayIntro);
    }

    [ContextMenu("PlayIntro")]
    private void TestPlayIntro() => PlayIntro(new EventContext());

    /// <summary>
    /// 触发邀请函入场、旋转、浮动以及语音淡出完整流程的公共接口
    /// </summary>
    private void PlayIntro(EventContext context)
    {
        // 清理上一次的无限浮动动画，防止叠加
        m_floatTween?.Kill();
        m_inviteGameObject.SetActive(true);

        // 重置状态与位置
        Color color = m_inviteImage.color;
        color.a = 1f;
        m_inviteImage.color = color;

        m_rectTransform.anchoredPosition = startAnchoredPos;
        m_rectTransform.localEulerAngles = Vector3.zero;

        // 计算下落所需的旋转角度
        float totalRotateAngle = (360f / rotateCycle) * fallTime;

        // 构建入场动画 Sequence
        Sequence introSeq = DOTween.Sequence();
        introSeq.Append(m_rectTransform.DOAnchorPos(endAnchoredPos, fallTime).SetEase(Ease.Linear));
        introSeq.Join(m_rectTransform.DOLocalRotate(new Vector3(0, 0, totalRotateAngle), fallTime, RotateMode.FastBeyond360).SetEase(Ease.Linear));

        introSeq.OnComplete(() =>
        {
            // 落地摆正 Z 轴
            m_rectTransform.localEulerAngles = new Vector3(0, 0, 0);

            // 启动无限往复浮动
            m_floatTween = m_rectTransform.DOAnchorPosY(endAnchoredPos.y + floatRange, floatSpeed / 2f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);

            // 播放语音淡出
            PlayVoiceAndFade();
        });

        introSeq.SetLink(gameObject);
    }

    private void PlayVoiceAndFade()
    {
        m_voiceAudio.Play();

        float totalTime = m_voiceAudio.clip.length;
        float fadeDelay = Mathf.Max(0f, totalTime - fadeStartBeforeEnd);

        // 使用 Sequence 处理淡出，并在完成后 Kill 掉浮动动画
        Sequence fadeSeq = DOTween.Sequence();
        fadeSeq.AppendInterval(fadeDelay);
        fadeSeq.Append(m_inviteImage.DOFade(0f, fadeDuration));
        fadeSeq.OnComplete(OnComplete);
        fadeSeq.SetLink(gameObject);
    }

    private void OnComplete()
    {
        Debug.Log("Invitation Anim Done.");
        m_floatTween?.Kill();
        director.time = 0.06;
        director.Play();
    }
}
