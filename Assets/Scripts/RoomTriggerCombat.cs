using UnityEngine;

/// <summary>
/// Placed on a GameObject with a Trigger Collider defining the room's bounds.
/// Triggers the transition to Combat Mode when the player enters.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RoomCombatTrigger : MonoBehaviour
{
    [Header("Encounter Settings")]
    [Tooltip("If true, this room will only trigger combat once per playthrough.")]
    public bool triggerOnlyOnce = true;

    private bool hasTriggered = false;
    private Collider triggerCollider;

    private void Start()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true; // Force it to be a trigger just in case
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnlyOnce) return;

        // Type-safe check: Is the entering object the Player?
        if (other.TryGetComponent(out PlayerController player))
        {
            hasTriggered = true;

            // Initiate the State Change
            GameModeManager.Instance.SwitchToCombat();

            Debug.Log($"[RoomCombatTrigger] Player entered {gameObject.name}. Switching to Combat Mode!");

            // Optional: If this is a one-time trigger, we can disable the collider 
            // to save physics engine calculations later.
            if (triggerOnlyOnce)
            {
                triggerCollider.enabled = false;
            }
            
            // TODO: Fire an event here to close/lock the room's doors!
        }
    }
}
