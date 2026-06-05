using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity音频管理器
/// 单AudioSource播放多个音效
/// 使用方法：挂载到场景中的游戏对象，在Inspector中配置音效
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class GameAudioManager : MonoBehaviour
{
    [Header("音频源配置")]
    [SerializeField] private AudioSource audioSource;

    [Header("音效列表")]
    [SerializeField] private List<SoundEntry> sounds = new List<SoundEntry>();

    [System.Serializable]
    public class SoundEntry
    {
        public string soundName;          // 音效名称（用于代码调用）
        public AudioClip audioClip;       // 音频文件
        [Range(0f, 1f)]
        public float volume = 1f;         // 音量大小
        [Range(0f, 0.5f)]
        public float pitchVariation = 0.1f; // 音高随机变化
    }

    [Header("调试设置")]
    [SerializeField] private bool enableLogging = true;
    [SerializeField] private bool initializeOnAwake = true;

    private Dictionary<string, SoundEntry> soundDictionary;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                Debug.Log($"{gameObject.name}: 已添加AudioSource组件");
            }
        }

        if (initializeOnAwake)
        {
            InitializeSoundDictionary();
        }
    }

    /// <summary>
    /// 初始化音效字典
    /// </summary>
    public void InitializeSoundDictionary()
    {
        soundDictionary = new Dictionary<string, SoundEntry>();

        foreach (SoundEntry entry in sounds)
        {
            if (string.IsNullOrEmpty(entry.soundName))
            {
                Debug.LogWarning("发现未命名的音效条目，已跳过");
                continue;
            }

            if (entry.audioClip == null)
            {
                Debug.LogWarning($"音效 '{entry.soundName}' 缺少音频文件，已跳过");
                continue;
            }

            if (soundDictionary.ContainsKey(entry.soundName))
            {
                Debug.LogWarning($"音效名称重复: {entry.soundName}，已覆盖");
            }

            soundDictionary[entry.soundName] = entry;
        }

        if (enableLogging)
        {
            Debug.Log($"音频管理器初始化完成，已加载 {soundDictionary.Count} 个音效");
        }
    }

    /// <summary>
    /// 播放音效
    /// </summary>
    /// <param name="soundName">音效名称</param>
    /// <param name="volumeMultiplier">音量乘数</param>
    /// <returns>是否成功播放</returns>
    public bool PlaySound(string soundName, float volumeMultiplier = 1f)
    {
        if (!soundDictionary.TryGetValue(soundName, out SoundEntry sound))
        {
            Debug.LogWarning($"未找到音效: {soundName}");
            return false;
        }

        if (sound.audioClip == null)
        {
            Debug.LogWarning($"音效 '{soundName}' 的音频文件为空");
            return false;
        }

        float finalVolume = Mathf.Clamp01(sound.volume * volumeMultiplier);
        audioSource.PlayOneShot(sound.audioClip, finalVolume);

        if (enableLogging)
        {
            Debug.Log($"播放音效: {soundName} (音量: {finalVolume})");
        }

        return true;
    }

    /// <summary>
    /// 播放音效（带随机音高）
    /// </summary>
    public bool PlaySoundWithRandomPitch(string soundName, float volumeMultiplier = 1f)
    {
        if (!soundDictionary.TryGetValue(soundName, out SoundEntry sound))
        {
            Debug.LogWarning($"未找到音效: {soundName}");
            return false;
        }

        float originalPitch = audioSource.pitch;
        float pitchVariation = Mathf.Clamp(sound.pitchVariation, 0f, 0.5f);

        // 应用随机音高
        audioSource.pitch = Random.Range(
            1f - pitchVariation,
            1f + pitchVariation
        );

        float finalVolume = Mathf.Clamp01(sound.volume * volumeMultiplier);
        audioSource.PlayOneShot(sound.audioClip, finalVolume);

        // 恢复原始音高
        audioSource.pitch = originalPitch;

        if (enableLogging)
        {
            Debug.Log($"播放音效(带音高变化): {soundName}");
        }

        return true;
    }

    /// <summary>
    /// 在指定位置播放音效（3D音效）
    /// </summary>
    public bool PlaySoundAtPosition(string soundName, Vector3 position, float volumeMultiplier = 1f)
    {
        if (!soundDictionary.TryGetValue(soundName, out SoundEntry sound))
        {
            Debug.LogWarning($"未找到音效: {soundName}");
            return false;
        }

        float finalVolume = Mathf.Clamp01(sound.volume * volumeMultiplier);
        AudioSource.PlayClipAtPoint(sound.audioClip, position, finalVolume);

        if (enableLogging)
        {
            Debug.Log($"在位置 {position} 播放3D音效: {soundName}");
        }

        return true;
    }

    /// <summary>
    /// 添加新音效
    /// </summary>
    public void AddSound(string name, AudioClip clip, float volume = 1f, float pitchVariation = 0.1f)
    {
        SoundEntry newSound = new SoundEntry
        {
            soundName = name,
            audioClip = clip,
            volume = volume,
            pitchVariation = pitchVariation
        };

        sounds.Add(newSound);

        if (soundDictionary != null)
        {
            soundDictionary[name] = newSound;
        }

        if (enableLogging)
        {
            Debug.Log($"添加新音效: {name}");
        }
    }

    /// <summary>
    /// 检查音效是否存在
    /// </summary>
    public bool HasSound(string soundName)
    {
        return soundDictionary != null && soundDictionary.ContainsKey(soundName);
    }

    /// <summary>
    /// 获取音效数量
    /// </summary>
    public int GetSoundCount()
    {
        return soundDictionary?.Count ?? 0;
    }

    /// <summary>
    /// 停止所有音效
    /// </summary>
    public void StopAllSounds()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// 设置静音
    /// </summary>
    public void SetMute(bool mute)
    {
        if (audioSource != null)
        {
            audioSource.mute = mute;
        }
    }

    /// <summary>
    /// 设置全局音量
    /// </summary>
    public void SetGlobalVolume(float volume)
    {
        if (audioSource != null)
        {
            audioSource.volume = Mathf.Clamp01(volume);
        }
    }

    /// <summary>
    /// 获取当前播放状态
    /// </summary>
    public bool IsPlaying()
    {
        return audioSource != null && audioSource.isPlaying;
    }
}
