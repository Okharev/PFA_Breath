using Ability.NewAbilitySystem;
using Dialogues.UI;
using System;
using TechArtPlayground.Oasis;
using UnityEngine;

public class DoorObjectives : MonoBehaviour
{
    [Tooltip("Number of door to trigger end of level")] 
    public int maxNbRoom;
    [Tooltip("Number of room unlocked by the player")] 
    public int currentNbRoom;

    [Header("Objectives level")]
    [Tooltip("Reference to the Fight Room 1 component")]
    [SerializeField]
    private EncounterRoomTrigger room1;
    [Tooltip("Reference to the Fight Room 2 component")]
    [SerializeField]
    private EncounterRoomTrigger room2;
    [Tooltip("Reference to the Dialogue Room")]
    [SerializeField]
    private DialogueDebugDirectTrigger dialogueRoom;

    public Action triggerEndLevel;
    public Action<int, int> onDoorNumberChanged;

    private void Awake()
    {
        room1.OnRoomCleared += AddRoomUnlocked;
        room2.OnRoomCleared += AddRoomUnlocked;
        dialogueRoom.onCleared += AddRoomUnlocked;
    }

    public void AddRoomUnlocked()
    {
        currentNbRoom++;

        onDoorNumberChanged?.Invoke(currentNbRoom, maxNbRoom);

        if (currentNbRoom >= maxNbRoom)
        {
            triggerEndLevel?.Invoke();
        }
    }
}
