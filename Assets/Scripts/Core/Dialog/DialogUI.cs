using Core.Dialog.Timeline;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Dialog
{
    /// <summary>
    /// 对话 UI 视图层：
    ///   - 由 Timeline 轨道绑定（拖入 Track Binding），无单例。
    ///   - 打字机进度由外部（Mixer）每帧推入，不使用协程，
    ///     确保与 Timeline 时间轴严格同步。
    ///   - 不处理任何玩家输入（跳过等），由 Timeline 完全控制节奏。
    /// </summary>
    public class DialogUI : MonoBehaviour
    {
        [Header("根节点（Show/Hide 控制）")]
        [SerializeField] private GameObject dialogPanel;
 
        [Header("文本组件")]
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text dialogueBodyText;
 
        [Header("立绘")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private GameObject portraitContainer; // 无立绘时整体隐藏
 
        [Header("语音")]
        [SerializeField] private AudioSource audioSource;
 
        [Header("打字机设置")]
        [Tooltip("打字机效果结束于 Clip 的哪个归一化时间点（0~1）。\n"
               + "例如 0.85 表示在 Clip 前 85% 时间内打完全部文字，\n"
               + "剩余 15% 时间（及 endPadding）用于阅读。")]
        [Range(0.1f, 1f)]
        [SerializeField] private float typewriterEndAt = 0.85f;
 
        // 当前显示的完整文本
        private string _fullText;
        
        // TODO:通用版本，暂时不管
        public void Show(DialogSO dialog)
        {
            if (dialog == null) return;
            
            dialogPanel.SetActive(true);
 
            characterNameText.text = dialog.characterName;
 
            // 先把文字设为空，等 UpdateTypewriter 逐步填入
            _fullText = dialog.dialogText ?? string.Empty;
            dialogueBodyText.text = string.Empty;
 
            // 立绘
            if (dialog.characterPortrait != null)
            {
                portraitImage.sprite = dialog.characterPortrait;
                if (portraitContainer) portraitContainer.SetActive(true);
            }
            else
            {
                if (portraitContainer) portraitContainer.SetActive(false);
            }
        }
 
        // ── 公开接口（由 DialogueBehaviour 调用）────────────────────────────
 
        /// <summary>Clip 开始时调用，展示面板并开始播放语音。</summary>
        public void Show(DialogLineSO line)
        {
            if (line == null) return;
            
            dialogPanel.SetActive(true);
 
            characterNameText.text = line.characterName;
 
            // 先把文字设为空，等 UpdateTypewriter 逐步填入
            _fullText = line.dialogText ?? string.Empty;
            dialogueBodyText.text = string.Empty;
 
            // 立绘
            if (line.characterPortrait != null)
            {
                portraitImage.sprite = line.characterPortrait;
                if (portraitContainer) portraitContainer.SetActive(true);
            }
            else
            {
                if (portraitContainer) portraitContainer.SetActive(false);
            }
 
            // 语音
            if (line.voiceClip != null && audioSource != null)
            {
                audioSource.clip = line.voiceClip;
                audioSource.Play();
            }
        }
 
        /// <summary>
        /// 每帧由 Mixer 推入归一化时间 [0,1]，驱动打字机。
        /// 不依赖协程，与 Timeline 时间轴完全同步。
        /// </summary>
        public void UpdateTypewriter(float normalizedTime)
        {
            if (string.IsNullOrEmpty(_fullText)) return;
 
            // 将 [0, typewriterEndAt] 重新映射到 [0, 1]
            float typeProgress = Mathf.Clamp01(normalizedTime / typewriterEndAt);
 
            int charCount = Mathf.RoundToInt(typeProgress * _fullText.Length);
            dialogueBodyText.text = _fullText[..charCount];
        }
 
        /// <summary>Clip 结束时调用，隐藏面板并停止语音。</summary>
        public void Hide()
        {
            dialogPanel.SetActive(false);
            _fullText = string.Empty;
 
            if (audioSource != null && audioSource.isPlaying)
                audioSource.Stop();
        }
        
        private void Awake()
        {
            var mat = new Material(Shader.Find("UI/Default"));
            mat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
    
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                graphic.material = mat;
            }
        }
    }
}