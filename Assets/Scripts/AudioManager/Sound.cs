using System;
using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]
public class Sound
{
    public enum typeAudio
    {
        player,
        other
    }

    public string nameMusic;

    public typeAudio typeMusic;

    public AudioClip clip;
    public AudioMixerGroup mixer;

    [Range(0f, 1f)]
    public float volume;
    [Range(0.1f, 3f)]
    public float pitch;
    [Range(0f, 1f)]
    public float spatialBlend;

    public bool loop;
    public bool playOnAwake;


    //[HideInInspector]
    public AudioSource source;

}
