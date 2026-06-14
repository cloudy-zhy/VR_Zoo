using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Core.Dialog.Timeline
{
    /// <summary>
    /// Timeline Clip 资产：绑定 DialogueLineSO，向 Graph 提供正确时长。
    /// </summary>
    [Serializable]
    public class DialogClip : PlayableAsset, ITimelineClipAsset
    {
        public DialogLineSO dialogLine;
        
        // Graph 构建期由 DialogueTrack.CreateTrackMixer 写入，运行时只读。
        // PlayableAsset 是 ScriptableObject，字段在 Graph 生命周期内持久，注入一次即可。
        private DialogUI m_ui;
        
        public void SetUI(DialogUI ui) => m_ui = ui;
 
        // ITimelineClipAsset：告知 Timeline 此 Clip 支持的功能
        // 跟随音频时不允许拉伸/循环，以免时长对不上
        public ClipCaps clipCaps
        {
            get
            {
                if (dialogLine == null) return ClipCaps.None;
                return dialogLine.useAudioDuration
                    ? ClipCaps.None
                    : ClipCaps.Extrapolation;
            }
        }
 
        /// <summary>
        /// Timeline 用此值初始化 Clip 的显示时长。
        /// Editor 脚本在 SO 变化时会触发 Clip 刷新以保持同步。
        /// </summary>
        public override double duration =>
            dialogLine != null ? dialogLine.GetTotalDuration() : 1.0;
 
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<DialogBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.dialogLine = dialogLine;
            behaviour.SetUI(m_ui);   // Graph 构建时一次性注入，Behaviour 持有引用
            return playable;
        }
    }
}