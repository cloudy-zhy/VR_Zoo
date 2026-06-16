using UnityEngine;

namespace Core.Dialog.Timeline
{
    /// <summary>
    /// 对话数据资产，一个实例对应 Timeline 中的一条对话 Clip。
    /// </summary>
    [CreateAssetMenu(fileName = "DialogLineSO", menuName = "Data/Dialog/DialogLineSO", order = 0)]
    public class DialogLineSO : ScriptableObject
    {
        [Header("内容")]
        public string characterName;
 
        [TextArea(2, 6)]
        public string dialogText;
 
        public Sprite characterPortrait;

        [Header("状态/表情")]
        [Tooltip("区分同一角色的不同状态或表情，默认为 default")]
        public string characterState = "default";
        
        [Header("语音")]
        public AudioClip voiceClip;
 
        [Header("时长设置")]
        [Tooltip("true = Clip 时长跟随音频；false = 使用固定时长")]
        public bool useAudioDuration = true;
 
        [Tooltip("固定时长模式下的显示秒数")]
        [Min(0.1f)]
        public float fixedDuration = 3f;
 
        [Tooltip("在计算出的时长末尾追加的缓冲秒数，避免音频/文字截断")]
        [Min(0f)]
        public float endPadding = 0.15f;
 
        /// <summary>
        /// 供 Clip / Editor 统一获取最终时长（含 padding）。
        /// </summary>
        public double GetTotalDuration()
        {
            double baseDuration = useAudioDuration ? voiceClip.length : fixedDuration;
            return baseDuration + endPadding;
        }
    }
}