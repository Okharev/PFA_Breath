using UnityEngine;

namespace TechArtPlayground.Oasis
{
    /// <summary>
    /// Connects an EncounterRoomTrigger to an OasisNode using the Observer Pattern.
    /// Ensures visual effects are decoupled from core gameplay logic.
    /// </summary>
    [RequireComponent(typeof(OasisNode))]
    public class EncounterOasisListener : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("The room that must be cleared to trigger this Oasis.")]
        [SerializeField] private EncounterRoomTrigger roomTrigger;
        
        private OasisNode oasisNode;

        private void Awake()
        {
            // Safely grab the required component on this GameObject
            oasisNode = GetComponent<OasisNode>();
        }

        private void OnEnable()
        {
            if (roomTrigger != null)
            {
                // Subscribe to the event when enabled
                roomTrigger.OnRoomCleared += HandleRoomCleared;
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] EncounterOasisListener is missing a RoomTrigger reference!");
            }
        }

        private void OnDisable()
        {
            if (roomTrigger != null)
            {
                // ALWAYS unsubscribe to prevent memory leaks and dangling references!
                roomTrigger.OnRoomCleared -= HandleRoomCleared;
            }
        }

        private void HandleRoomCleared()
        {
            Debug.Log($"[{gameObject.name}] Room cleared event received. Triggering Oasis.");
            
            // Activate the expanding healing wave
            oasisNode.RetractOasis();
        }
    }
}