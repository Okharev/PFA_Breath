using System;
using System.Collections.Generic;
using Ability.NewAbilitySystem;
using TechArtPlayground.Wind;
using UnityEngine;
// Required to access GameModeManager

[Serializable]
public struct EnemySpawnData
{
    public Transform SpawnPoint;
    public GameObject EnemyPrefab;
}

[RequireComponent(typeof(Collider))]
public class EncounterRoomTrigger : MonoBehaviour
{
    [Header("Room Settings")] public bool isCleared;

    public bool autoClearOnEnemiesDefeated = true;

    [Header("Doors")] [SerializeField] private List<DoorController> roomDoors = new();

    [Header("Encounter Configuration")] [SerializeField]
    private List<EnemySpawnData> enemySpawns = new();

    // DSA OPTIMIZATION: HashSet provides O(1) insertion and removal
    private readonly HashSet<GameObject> activeEnemies = new();

    // REMOVED: [SerializeField] private OceanWeatherController weatherController;
    // We no longer need a localized reference. We rely on the global state.

    // Track if the encounter is currently running
    private bool isEncounterActive;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    // --- QUALITY OF LIFE: Level Designer Tooling ---
    private void OnDrawGizmosSelected()
    {
        if (enemySpawns == null || enemySpawns.Count == 0) return;

        foreach (EnemySpawnData spawn in enemySpawns)
            if (spawn.SpawnPoint is not null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                Gizmos.DrawSphere(spawn.SpawnPoint.position, 0.5f);

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, spawn.SpawnPoint.position);

                Gizmos.color = Color.blue;
                Gizmos.DrawRay(spawn.SpawnPoint.position, spawn.SpawnPoint.forward * 1.5f);
            }
    }

    private void OnTriggerEnter(Collider other)
    {
        // GUARD CLAUSE: Ignore if we already beat it, OR if we are currently fighting!
        if (isCleared || isEncounterActive) return;

        if (other.CompareTag("Player")) InitiateEncounter();
    }

    private void InitiateEncounter()
    {
        Debug.Log($"[EncounterRoom] Player entered {gameObject.name}. Initiating Combat!");

        // 1. Lock state and disable physics checks for this trigger
        isEncounterActive = true;
        GetComponent<Collider>().enabled = false;

        foreach (DoorController door in roomDoors)
            if (door != null)
                door.CloseDoor();

        // =========================================================
        // UPDATED: Broadcast to the Global Weather Manager
        // =========================================================
        if (GlobalWeatherManager.Instance != null)
            // Transition to Tempest over 5 seconds
            GlobalWeatherManager.Instance.TransitionToTempest();

        GameModeManager.Instance.SetGameMode(GameMode.Combat);
        SpawnEnemies();
    }

    private void SpawnEnemies()
    {
        foreach (EnemySpawnData spawnData in enemySpawns)
        {
            if (spawnData.SpawnPoint == null || spawnData.EnemyPrefab == null) continue;

            GameObject spawnedEnemy = Instantiate(
                spawnData.EnemyPrefab,
                spawnData.SpawnPoint.position,
                spawnData.SpawnPoint.rotation
            );

            activeEnemies.Add(spawnedEnemy);

            // OBSERVER PATTERN: Subscribe to the death event safely
            if (spawnedEnemy.TryGetComponent(out HealthComponent health)) health.OnDeath += HandleEnemyDeath;
        }
    }

    public void HandleEnemyDeath(GameObject deadEnemy)
    {
        // O(1) Removal
        activeEnemies.Remove(deadEnemy);

        // OBSERVER PATTERN: Always unsubscribe to prevent memory leaks!
        if (deadEnemy.TryGetComponent(out HealthComponent health)) health.OnDeath -= HandleEnemyDeath;

        if (autoClearOnEnemiesDefeated && activeEnemies.Count == 0) ResolveEncounter();
    }

    public event Action OnRoomCleared;

    public void ResolveEncounter()
    {
        isCleared = true;
        Debug.Log("[EncounterRoom] Room cleared. Returning to Exploration.");

        foreach (DoorController door in roomDoors)
            if (door != null)
                door.OpenDoor();

        // =========================================================
        // UPDATED: Broadcast to the Global Weather Manager
        // =========================================================
        if (GlobalWeatherManager.Instance != null)
            // Transition back to Calm over 10 seconds
            GlobalWeatherManager.Instance.TransitionToCalm();

        // Switch back to Exploration mode
        GameModeManager.Instance.SetGameMode(GameMode.Exploration);

        // OBSERVER PATTERN: Notify all subscribed listeners that the room is cleared
        OnRoomCleared?.Invoke();
    }
}