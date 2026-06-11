using UnityEngine;
[System.Serializable]
public class spatialObject
{
    public string name;
    public Transform[] transforms;
}

public class SpatialAudioManager : MonoBehaviour
{
    private AudioManager audioManager;

    public spatialObject[] spatialObjects;

    void Start()
    {

        audioManager = AudioManager.instance;


        if (audioManager == null || spatialObjects == null) return;
        Debug.Log("There is an audio manager and spatial objects");

        for (int i = 0; i < spatialObjects.Length; i++)
        {

            Sound s = audioManager.GetSound(spatialObjects[i].name);
            if (s != null)
            {
                Debug.Log("The sound " + spatialObjects[i].name + " was found");

                //audioManager.Play(s.nameMusic);

                foreach (Transform obj in spatialObjects[i].transforms)
                {

                    AudioSource source = obj.gameObject.AddComponent<AudioSource>();
                    source.clip = s.clip;
                    source.outputAudioMixerGroup = s.mixer;
                    source.volume = s.volume;
                    source.pitch = s.pitch;
                    source.loop = s.loop;
                    source.spatialBlend = s.spatialBlend;
                    source.playOnAwake = s.playOnAwake;
                    source.minDistance = s.minDistance;
                    source.Play();
                    Debug.Log("We add audio source component");

                }

            }
        }


      
    }

}
