using UnityEngine;
using UnityEngine.Playables;

namespace Core.Dialog.Timeline
{
    /// <summary>
    /// 运行时行为：负责与 DialogueUI 通信，驱动打字机进度。
    /// </summary>
    public class DialogBehaviour : PlayableBehaviour
    {
        public DialogLineSO dialogLine;

        // 由 DialogClip 传入的该 Clip 的专属播放 AudioSource 引用
        public AudioSource targetAudioSource;
 
        // 由 DialogueTrackMixer 注入，避免单例
        private DialogUI m_boundUI;
 
        // 防止 OnBehaviourPause 在未播放时误触发
        private bool m_hasShown;
 
        // ── 由 DialogueClip 在 Graph 构建期调用一次 ────────────────────────
        public void SetUI(DialogUI ui) => m_boundUI = ui;
 
        // ── Playable 生命周期 ─────────────────────────────────────────────
 
        public override void OnGraphStart(Playable playable)
        {
            m_hasShown = false;
        }
 
        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            // EditMode 保护：编辑器 Scrub 时不触发真实逻辑
            if (!Application.isPlaying) return;
            if (dialogLine == null || m_boundUI == null) return;
 
            // 语音：targetAudioSource 故意不做空检查，确保在测试时漏配能立刻报错
            m_boundUI.Show(dialogLine, targetAudioSource);
            m_hasShown = true;
        }
 
        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (!Application.isPlaying) return;
            if (!m_hasShown || m_boundUI == null) return;
 
            // 语音：targetAudioSource 故意不做空检查，确保在测试时漏配能立刻报错
            m_boundUI.Hide(targetAudioSource);
            m_hasShown = false;
        }
 
        /// <summary>
        /// 由 Mixer 每帧调用，传入归一化进度 [0,1] 驱动打字机。
        /// </summary>
        public void OnMixerUpdate(float normalizedTime, double clipTime, double clipDuration)
        {
            if (!Application.isPlaying) return;
            if (dialogLine == null || m_boundUI == null) return;

            // 如果已经播放完毕（适用于 Hold 模式等未触发 OnBehaviourPause 的情况）
            if (m_hasShown && clipTime >= clipDuration - 0.005)
            {
                m_boundUI.Hide(targetAudioSource);
                m_hasShown = false;
                return;
            }

            if (!m_hasShown) return;

            m_boundUI.UpdateTypewriter(normalizedTime);
        }
    }
}