using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]
public class AudioType
{
    [HideInInspector]
    public AudioSource Source;
    public AudioClip Clip;
    public AudioMixerGroup Group;

    public string Name;
    [Range(0f, 1f)] public float Volume = 1f;
    [Range(0.1f, 5f)] public float Pitch = 1f;
    public bool Loop = false;
    [Range(0f, 1f)] public float SpatialBlend = 1f;
}