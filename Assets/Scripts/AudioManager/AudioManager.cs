using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{

    public static AudioManager instance;

    public Sound[] sounds;
    [SerializeField] AudioMixer audioMixer;

    // Une liste des musique qui sont en cours de lecture
    // Faire pause à ces musiques si on est en pause

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        foreach (Sound s in sounds)
        {
            if (s.typeMusic == Sound.typeAudio.player)
            {
                s.source = gameObject.AddComponent<AudioSource>();
                AddToSource(s);
            }
            //s.source = gameObject.AddComponent<AudioSource>();

        }

    }

    private void AddToSource(Sound s)
    {
        s.source.clip = s.clip;
        s.source.outputAudioMixerGroup = s.mixer;
        s.source.volume = s.volume;
        s.source.pitch = s.pitch;
        s.source.loop = s.loop;
        s.source.spatialBlend = s.spatialBlend;
        s.source.playOnAwake = s.playOnAwake;
    }

    public void Play(string name)
    {
        Sound s = Array.Find(sounds, sound => sound.nameMusic == name);
        if (s == null)
        {
            Debug.Log("didn't find the sound");
            return;
        }


        //if (s.typeMusic == Sound.typeAudio.player)
        //{

        //}
        //else
        //{
        //    s.source = actor.gameObject.AddComponent<AudioSource>();
        //    AddToSource(s);
        //}

        Debug.Log("the sound was found");
        s.source.Play();

    }

    public void SetMainVolume(float volume)
    {
        audioMixer.SetFloat("musicVolume", MathF.Log10(volume) * 20f);
    }

    public void SetEffectVolume(float volume)
    {
        audioMixer.SetFloat("effectVolume", Mathf.Log10(volume) * 20f);
    }


    public IEnumerator PlayOnActor(string name, GameObject actor)
    {
        Sound sound = GetSound(name);

        sound.source = actor.gameObject.AddComponent<AudioSource>();
        AddToSource(sound);
        sound.source.Play();

        yield return new WaitForSeconds(sound.clip.length);

        Destroy(actor.GetComponent<AudioSource>());
    }

    public Sound GetSound(string name)
    {
        Sound s = Array.Find(sounds, sound => sound.nameMusic == name);
        if (s == null)
        {
            return null;
        }


        return s;

    }



}
