using UnityEngine;
using UnityEngine.Playables;

namespace Core.Dialog.Timeline
{
    /// <summary>
    /// Mixer Behaviour：
    ///   1. 以归一化时间驱动当前激活 Clip 的打字机进度。
    ///   2. UI 引用已在 CreateTrackMixer 中注入，此处不再处理。
    ///   3. 约定单轨道，取 weight > 0 的首个 Input 处理（无混合逻辑）。
    /// </summary>
    public class DialogTrackMixer : PlayableBehaviour
    {
        public override void ProcessFrame(
            Playable playable, FrameData info, object playerData)
        {
            // EditMode 保护
            if (!Application.isPlaying) return;
 
            int inputCount = playable.GetInputCount();
            int activeIndex = -1;
 
            for (int i = 0; i < inputCount; i++)
            {
                if (playable.GetInputWeight(i) > 0f)
                {
                    activeIndex = i;
                    break;
                }
            }
 
            if (activeIndex < 0) return;
 
            var inputPlayable = (ScriptPlayable<DialogBehaviour>)
                playable.GetInput(activeIndex);
            var behaviour = inputPlayable.GetBehaviour();
 
            // 计算归一化时间 [0, 1]，clamp 防止浮点越界
            double clipDuration = inputPlayable.GetDuration();
            double clipTime     = inputPlayable.GetTime();
            float normalized = clipDuration > 0
                ? Mathf.Clamp01((float)(clipTime / clipDuration))
                : 1f;
 
            behaviour.OnMixerUpdate(normalized);
        }
    }
}