using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a specific combat room. Tracks enemies and returns the game to Exploration mode when cleared.
/// </summary>
public class EncounterManager : MonoBehaviour
{
    [Header("Encounter Setup")]
    [Tooltip("Drag the enemies in this room into this list.")]
    public List<HealthComponent> enemiesInRoom = new List<HealthComponent>();

    private int remainingEnemies;
    private bool isEncounterActive = false;

    private void Start()
    {
        remainingEnemies = enemiesInRoom.Count;

        // Subscribe to the death event of every enemy in this room
        foreach (var enemy in enemiesInRoom)
        {
            if (enemy != null)
            {
                enemy.OnDeath += HandleEnemyDeath;
            }
        }
    }

    /// <summary>
    /// Called by the RoomCombatTrigger when the player enters the room.
    /// </summary>
    public void StartEncounter()
    {
        if (remainingEnemies > 0 && !isEncounterActive)
        {
            isEncounterActive = true;
            GameModeManager.Instance.SwitchToCombat();
            Debug.Log($"[EncounterManager] Encounter started! Enemies remaining: {remainingEnemies}");
        }
    }

    private void HandleEnemyDeath(HealthComponent deadEnemy)
    {
        // Unsubscribe to prevent memory leaks
        deadEnemy.OnDeath -= HandleEnemyDeath;
        
        remainingEnemies--;
        Debug.Log($"[EncounterManager] Enemy defeated! Remaining: {remainingEnemies}");

        if (remainingEnemies <= 0 && isEncounterActive)
        {
            EndEncounter();
        }
    }

    private void EndEncounter()
    {
        isEncounterActive = false;
        
        // Return the player to real-time movement!
        GameModeManager.Instance.SwitchToExploration();
        
        Debug.Log("[EncounterManager] Room cleared! Returning to Exploration mode.");
    }

    private void OnDestroy()
    {
        // Safety cleanup in case the room is destroyed before enemies are killed
        foreach (var enemy in enemiesInRoom)
        {
            if (enemy != null)
            {
                enemy.OnDeath -= HandleEnemyDeath;
            }
        }
    }
}