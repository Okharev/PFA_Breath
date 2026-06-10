using UnityEngine;

public class AudioTriggerRoom : MonoBehaviour
{
    private AudioManager _audioManager;
    private EncounterRoomTrigger room;

    public string enteringSound;
    public string clearingSound;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _audioManager = AudioManager.instance;

        room = GetComponent<EncounterRoomTrigger>();
        room.OnRoomCleared += SoundClearingRoom;
        room.OnRoomTriggered += SoungTriggering;
    }

    // Update is called once per frame
    void SoundClearingRoom()
    {
        if (clearingSound == null) return;
        _audioManager.Play(clearingSound);
    }

    void SoungTriggering()
    {
        if(enteringSound == null) return;
        _audioManager.Play(enteringSound);
    }
}
