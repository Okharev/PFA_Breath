using Dialogues;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

namespace RoomBuilding
{
    /// <summary>
    /// Acts as a Bridge (Observer Pattern) connecting the combat encounter system
    /// to the room rebuilding system without tightly coupling them.
    /// Tracks asynchronous rebuild states to batch heavy NavMesh operations.
    /// </summary>
    public class EncounterRebuildListener : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("The trigger that detects when the encounter is finished.")]
        [SerializeField] private EncounterRoomTrigger _encounterTrigger;
        [SerializeField] private DialogueManager _dialogueManager;
        [SerializeField] private List<RoomEncounterController> _roomControllers;
        [SerializeField] private NavMeshSurface _navMeshSurface;

        // Tracks how many rooms are currently running their rebuild animations.
        private int _activeRebuilds = 0;

        private void OnEnable()
        {
            // 1. Subscribe to Encounter and Dialogue triggers
            if (_encounterTrigger != null)
                _encounterTrigger.OnRoomCleared += HandleRoomCleared;
            else
                Debug.LogWarning($"[EncounterRebuildListener] EncounterTrigger is missing on {gameObject.name}!");

            if (_dialogueManager != null) 
                _dialogueManager.OnConversationEnded += HandleRoomCleared;

            // 2. Subscribe to Room Rebuilders (Observer Pattern)

            foreach (RoomEncounterController room in _roomControllers)
            {
                if (room != null)
                {
                    // FIX: Guarantee we have the reference even if Awake() hasn't fired yet
                    if (room._rebuilder == null) 
                        room._rebuilder = room.GetComponent<RoomRebuilder>();

                    if (room._rebuilder != null)
                    {
                        room._rebuilder.OnRebuildComplete += HandleSingleRoomRebuildComplete;
                        Debug.Log($"[EncounterRebuildListener] Successfully subscribed to {room.gameObject.name}");
                    }
                }
            }
        }

        private void OnDisable()
        {
            // 1. Unsubscribe from triggers
            if (_encounterTrigger != null)
                _encounterTrigger.OnRoomCleared -= HandleRoomCleared;
            
            if (_dialogueManager != null) 
                _dialogueManager.OnConversationEnded -= HandleRoomCleared;

            // 2. CRITICAL: Unsubscribe from Room Rebuilders to prevent memory leaks
            // Time Complexity: O(N) where N is the number of room controllers.
            foreach (RoomEncounterController room in _roomControllers)
            {
                if (room != null && room._rebuilder != null)
                {
                    room._rebuilder.OnRebuildComplete -= HandleSingleRoomRebuildComplete;
                }
            }
        }

        /// <summary>
        /// Event handler invoked when the EncounterRoomTrigger broadcasts completion.
        /// </summary>
        private void HandleRoomCleared()
        {
            Debug.Log("[EncounterRebuildListener] Encounter cleared event received. Triggering rebuild sequence.");
            
            // If there are no rooms to rebuild, just bake immediately and exit.
            if (_roomControllers.Count == 0)
            {
                BakeNavMesh();
                return;
            }

            // Execute the rebuilding sequence
            foreach (RoomEncounterController room in _roomControllers)
            {
                _activeRebuilds++; // Increment our state tracker
                room.OnRoomCleared();
            }
        }

        /// <summary>
        /// Fired by individual RoomRebuilders when their animation sequence is 100% complete.
        /// </summary>
        private void HandleSingleRoomRebuildComplete()
        {
            _activeRebuilds--;

            // Once all asynchronous rebuild operations have finished, we execute the heavy bake.
            if (_activeRebuilds <= 0)
            {
                _activeRebuilds = 0; // Failsafe clamp
                BakeNavMesh();
            }
        }   

        private void BakeNavMesh()
        {
            Debug.Log("[EncounterRebuildListener] All room animations complete. Baking NavMesh.");
            
            if (_navMeshSurface != null)
            {
                _navMeshSurface.BuildNavMesh(); 
            }
            else
            {
                Debug.LogError("[EncounterRebuildListener] NavMeshSurface is missing! Cannot rebuild navigation.");
            }
        }
    }
}