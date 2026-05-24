using System;
using UnityEngine;

/// <summary>
/// 全局音管理类
/// 没有dialogue
/// </summary>
public class AudioManagerGlobal : PersistentSingleton<AudioManagerGlobal>
{
    public AudioType[] AudioTypes;

    private void Start()
    {
        foreach (AudioType type in AudioTypes)
        {
            if (type.SpatialBlend != 0)
                continue; //空间音效不进行挂载
            
            type.Source = gameObject.AddComponent<AudioSource>();
            
            type.Source.clip = type.Clip;
            type.Source.name = type.Name;
            type.Source.volume = type.Volume;
            type.Source.pitch = type.Pitch;
            type.Source.loop = type.Loop;
            type.Source.spatialBlend = type.SpatialBlend;
            
            if (type.Group != null)
            {
                type.Source.outputAudioMixerGroup = type.Group;
            }
        }
    }

    // 非空间音调用该方法
    public void Play(string name)
    {
        foreach (AudioType type in AudioTypes)
        {
            if (type.Name == name)
            {
                type.Source.Play();
                return;
            }
        }
        Debug.LogError("AudioManagerGlobal.Play: " + name + " not found");
    }
    public void Pause(string name)
    {
        foreach (AudioType type in AudioTypes)
        {
            if (type.Name == name)
            {
                type.Source.Pause();
                return;
            }
        }
        Debug.LogError("AudioManagerGlobal.Pause: " + name + " not found");
    }
    public void Stop(string name)
    {
        foreach (AudioType type in AudioTypes)
        {
            if (type.Name == name)
            {
                type.Source.Stop();
                return;
            }
        }
        Debug.LogError("AudioManagerGlobal.Stop: " + name + " not found");
    }

    // 空间音用该方法
    public void PlayInThisGameObject(string name, GameObject gameObject)
    {
        foreach (AudioType type in AudioTypes)
        {
            if (type.Name == name)
            {
                type.Source = gameObject.AddComponent<AudioSource>();

                type.Source.clip = type.Clip;
                type.Source.name = type.Name;
                type.Source.volume = type.Volume;
                type.Source.pitch = type.Pitch;
                type.Source.loop = type.Loop;
                type.Source.spatialBlend = type.SpatialBlend;

                if (type.Group != null)
                {
                    type.Source.outputAudioMixerGroup = type.Group;
                }

                type.Source.Play();
                return;
            }
        }
    }
}