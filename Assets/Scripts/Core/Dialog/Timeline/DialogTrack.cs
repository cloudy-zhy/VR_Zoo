using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Core.Dialog.Timeline
{
    /// <summary>
    /// 对话轨道：绑定对象为 DialogUI（在 Timeline 面板左侧槽拖入）。
    /// 约定：一个 PlayableDirector 中仅使用一条此轨道。
    /// </summary>
    [TrackColor(0.18f, 0.58f, 0.98f)]
    [TrackClipType(typeof(DialogClip))]
    [TrackBindingType(typeof(DialogUI))]
    public class DialogTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(
            PlayableGraph graph, GameObject go, int inputCount)
        {
            // Graph 构建期只执行一次：取绑定的 DialogUI，注入所有 Clip 的 Behaviour 与 Mixer。
            // Behaviour 实例在 Graph 生命周期内固定不变，注入一次即可，无需每帧重复。
            var director = go.GetComponent<PlayableDirector>();
            var ui = director != null
                ? director.GetGenericBinding(this) as DialogUI
                : null;
 
            foreach (var clip in GetClips())
            {
                if (clip.asset is DialogClip dialogClip)
                    dialogClip.SetUI(ui);
            }
 
            var mixerPlayable = ScriptPlayable<DialogTrackMixer>.Create(graph, inputCount);
            var mixer = mixerPlayable.GetBehaviour();
            mixer.SetUI(ui);
 
            return mixerPlayable;
        }
    }
}