using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AudioConfiguration", menuName = "Audio/AudioConfiguration")]
public class AudioConfiguration : ScriptableObject
{
    public AudioType[] audioTypes;
}
