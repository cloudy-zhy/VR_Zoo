using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace Core.Dialog.Timeline
{
    /// <summary>
    /// 自定义 DialogClip 的 Timeline 编辑器：
    ///   1. OnClipChanged：SO 变化时自动同步 Clip 时长。
    ///   2. GetClipOptions：自定义 Clip 标签显示角色名和文本预览。
    /// </summary>
    [CustomTimelineEditor(typeof(DialogClip))]
    public class DialogClipEditor : ClipEditor
    {
        // 缓存上次的时长，避免每帧都写入（Editor 性能优化）
        private double _lastKnownDuration = -1;
        private DialogLineSO _lastSO;
 
        /// <summary>
        /// Timeline 编辑器每帧刷新时调用，同步 Clip 时长与显示名称。
        /// </summary>
        public override void OnClipChanged(TimelineClip clip)
        {
            if (clip.asset is not DialogClip dialogClip) return;
            var so = dialogClip.dialogLine;
            if (so == null) return;
 
            double desiredDuration = so.GetTotalDuration();
 
            // 仅在 SO 或时长真正发生变化时更新，避免无谓的 dirty
            if (so == _lastSO && Mathf.Approximately((float)desiredDuration, (float)_lastKnownDuration))
                return;
 
            _lastSO = so;
            _lastKnownDuration = desiredDuration;
 
            // 同步时长
            clip.duration = desiredDuration;
 
            // 同步 Clip 标签：直接写 displayName，这是 Timeline 唯一支持的文字入口
            string preview = string.IsNullOrEmpty(so.dialogText)
                ? ""
                : so.dialogText.Length > 20
                    ? so.dialogText[..20] + "…"
                    : so.dialogText;
 
            clip.displayName = string.IsNullOrEmpty(so.characterName)
                ? preview
                : $"{so.characterName}：{preview}";
 
            // 通知 Timeline 窗口重绘
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
        }
    }
}