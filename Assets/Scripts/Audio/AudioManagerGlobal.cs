using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局音管理类
/// </summary>
public class AudioManagerGlobal : PersistentSingleton<AudioManagerGlobal>
{
    private Dictionary<string, AudioType> audioDictionary = new Dictionary<string, AudioType>();
    public AudioConfiguration audioConfig;

    private void Start()
    {
        audioDictionary.Clear();
        foreach (AudioType type in audioConfig.audioTypes)
        {
            audioDictionary.Add(type.Name, type);
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
        if(audioDictionary.TryGetValue(name, out AudioType type))
        {
            if (type.Source != null)
            {
                type.Source.Play();
            }
            else
            {
                PlayInThisGameObject(name, gameObject);
            }
            return;
        }
        Debug.LogError("AudioManagerGlobal.Play: " + name + " not found");
    }
    public void Pause(string name)
    {
        if(audioDictionary.TryGetValue(name, out AudioType type))
        {
            type.Source.Pause();
            return;
        }
        Debug.LogError("AudioManagerGlobal.Pause: " + name + " not found");
    }
    public void Stop(string name)
    {
        if(audioDictionary.TryGetValue(name, out AudioType type))
        { 
            type.Source.Stop(); 
            return;
        }
        Debug.LogError("AudioManagerGlobal.Stop: " + name + " not found");
    }

    // 空间音用该方法
    public void PlayInThisGameObject(string name, GameObject gameObject)
    {
        if(audioDictionary.TryGetValue(name, out AudioType type))
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