using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EncounterRoomTrigger : MonoBehaviour
{
    [Header("Room Settings")]
    public bool isCleared = false;
    
    [Tooltip("If true, automatically switches back to exploration when all enemies are defeated.")]
    public bool autoClearOnEnemiesDefeated = true;

    private void Awake()
    {
        // Ensure the collider is a trigger
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCleared) return;

        // Check against the player's tag or layer (assume Tag for this example)
        if (other.CompareTag("Player"))
        {
            InitiateEncounter();
        }
    }

    private void InitiateEncounter()
    {
        Debug.Log($"[EncounterRoom] Player entered {gameObject.name}. Switching to Combat!");
        
        // Stop the player's NavMesh agent in its tracks
        var playerCtrl = FindObjectOfType<PlayerController>(); // Or use a PlayerManager pattern
        if (playerCtrl != null) playerCtrl.StopMovement();

        // Lock into turn-based combat
        GameModeManager.Instance.SwitchToCombat();
        
        // TODO: Spawn/Activate enemies here.
        // If using an EnemyManager, you might subscribe to an OnAllEnemiesDefeated event
        // to call ResolveEncounter().
    }

    public void ResolveEncounter()
    {
        isCleared = true;
        Debug.Log($"[EncounterRoom] Room cleared. Returning to Exploration.");
        GameModeManager.Instance.SwitchToExploration();
    }
}