using System.Collections.Generic; // 如果需要使用List
using UnityEngine;

/// <summary>
/// 单AudioSource多音效管理器
/// 使用方法：将此脚本挂载到带有AudioSource组件的物体上
/// 在Inspector中配置音效文件，然后在其他脚本中调用相应的方法
/// </summary>
[RequireComponent(typeof(AudioSource))] // 自动添加AudioSource组件
public class SoundManager : MonoBehaviour
{
    [Header("音频源配置")]
    [SerializeField] private AudioSource audioSource; // 主音频源

    [Header("音效列表")]
    [SerializeField] private List<SoundEntry> sounds = new List<SoundEntry>();

    [System.Serializable]
    public class SoundEntry
    {
        public string soundName;     // 音效名称
        public AudioClip audioClip;  // 音频片段
        [Range(0f, 1f)] public float volume = 1f; // 音量
    }

    [Header("调试设置")]
    [SerializeField] private bool enableLogging = true; // 是否启用调试日志

    private Dictionary<string, SoundEntry> soundDictionary; // 音效字典，快速查找

    private void Awake()
    {
        // 自动获取AudioSource组件
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                if (enableLogging) Debug.Log($"{gameObject.name}: 添加了AudioSource组件");
            }
        }

        // 初始化音效字典
        InitializeSoundDictionary();
    }

    /// <summary>
    /// 初始化音效字典
    /// </summary>
    private void InitializeSoundDictionary()
    {
        soundDictionary = new Dictionary<string, SoundEntry>();

        foreach (SoundEntry entry in sounds)
        {
            if (!string.IsNullOrEmpty(entry.soundName) && entry.audioClip != null)
            {
                if (!soundDictionary.ContainsKey(entry.soundName))
                {
                    soundDictionary.Add(entry.soundName, entry);
                }
                else
                {
                    Debug.LogWarning($"音效名称重复: {entry.soundName}");
                }
            }
        }

        if (enableLogging) Debug.Log($"已加载 {soundDictionary.Count} 个音效");
    }

    /// <summary>
    /// 播放指定名称的音效
    /// </summary>
    /// <param name="soundName">音效名称</param>
    public void PlaySound(string soundName)
    {
        if (soundDictionary.TryGetValue(soundName, out SoundEntry sound))
        {
            audioSource.PlayOneShot(sound.audioClip, sound.volume);

            if (enableLogging) Debug.Log($"播放音效: {soundName}");
        }
        else
        {
            Debug.LogWarning($"未找到音效: {soundName}");
        }
    }

    /// <summary>
    /// 播放指定名称的音效（带音高变化）
    /// </summary>
    /// <param name="soundName">音效名称</param>
    /// <param name="pitchVariation">音高变化范围（0-1）</param>
    public void PlaySoundWithPitch(string soundName, float pitchVariation = 0.1f)
    {
        if (soundDictionary.TryGetValue(soundName, out SoundEntry sound))
        {
            // 保存原始音高
            float originalPitch = audioSource.pitch;

            // 设置随机音高
            audioSource.pitch = Random.Range(
                originalPitch - pitchVariation,
                originalPitch + pitchVariation
            );

            // 播放音效
            audioSource.PlayOneShot(sound.audioClip, sound.volume);

            // 恢复原始音高
            audioSource.pitch = originalPitch;

            if (enableLogging) Debug.Log($"播放音效(带音高变化): {soundName}");
        }
        else
        {
            Debug.LogWarning($"未找到音效: {soundName}");
        }
    }

    /// <summary>
    /// 播放指定名称的音效（带位置信息，适用于3D音效）
    /// </summary>
    /// <param name="soundName">音效名称</param>
    /// <param name="position">播放位置</param>
    public void PlaySoundAtPosition(string soundName, Vector3 position)
    {
        if (soundDictionary.TryGetValue(soundName, out SoundEntry sound))
        {
            AudioSource.PlayClipAtPoint(sound.audioClip, position, sound.volume);

            if (enableLogging) Debug.Log($"在位置 {position} 播放音效: {soundName}");
        }
        else
        {
            Debug.LogWarning($"未找到音效: {soundName}");
        }
    }

    /// <summary>
    /// 添加新音效
    /// </summary>
    public void AddSound(string name, AudioClip clip, float volume = 1f)
    {
        SoundEntry newSound = new SoundEntry
        {
            soundName = name,
            audioClip = clip,
            volume = volume
        };

        sounds.Add(newSound);

        // 更新字典
        if (!soundDictionary.ContainsKey(name))
        {
            soundDictionary.Add(name, newSound);
        }

        if (enableLogging) Debug.Log($"添加了新音效: {name}");
    }

    /// <summary>
    /// 停止所有音效
    /// </summary>
    public void StopAllSounds()
    {
        audioSource.Stop();
    }

    /// <summary>
    /// 设置音频源静音
    /// </summary>
    public void SetMute(bool mute)
    {
        audioSource.mute = mute;
    }

    /// <summary>
    /// 设置主音量
    /// </summary>
    public void SetVolume(float volume)
    {
        audioSource.volume = Mathf.Clamp01(volume);
    }
}