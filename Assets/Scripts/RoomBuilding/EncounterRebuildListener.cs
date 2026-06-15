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
    /// </summary>
    public class EncounterRebuildListener : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("The trigger that detects when the encounter is finished.")]
        [SerializeField] private EncounterRoomTrigger _encounterTrigger;

        [SerializeField] private List<RoomEncounterController> _roomControllers;

        [SerializeField] private NavMeshSurface navMeshSurface;

        private void OnEnable()
        {
            // Guard clause to prevent NullReferenceExceptions
            if (_encounterTrigger != null)
            {
                // Subscribe to the event. 
                // Time Complexity: O(1) delegate addition
                _encounterTrigger.OnRoomCleared += HandleRoomCleared;
                

            }
            else
            {
                Debug.LogWarning($"[EncounterRebuildListener] {_encounterTrigger} is missing on {gameObject.name}!");
            }
        }

        private void Start()
        {
            foreach (RoomEncounterController room in _roomControllers)
            {
                //room._rebuilder.OnRebuildComplete += RebuildRoom;
            }
        }

        private void OnDisable()
        {
            if (_encounterTrigger != null)
            {
                // CRITICAL: Always unsubscribe in OnDisable to prevent memory leaks 
                // and dangling references when objects are destroyed.
                // Time Complexity: O(1) delegate removal
                _encounterTrigger.OnRoomCleared -= HandleRoomCleared;
            }
        }

        /// <summary>
        /// Event handler invoked when the EncounterRoomTrigger broadcasts completion.
        /// </summary>
        private void HandleRoomCleared()
        {
            Debug.Log("[EncounterRebuildListener] Encounter cleared event received. Triggering rebuild.");
            
            // Execute the rebuilding sequence
            foreach (RoomEncounterController room in _roomControllers)
            {
                room.OnRoomCleared();
            }


            StartCoroutine(RebuildRoom());
        }

        private IEnumerator RebuildRoom()
        {
            yield return new WaitForSeconds(5f);
            
            //navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
            navMeshSurface.BuildNavMesh(); 
        }
    }
}