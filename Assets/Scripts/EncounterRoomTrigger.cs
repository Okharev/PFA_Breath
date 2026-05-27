using System.Collections.Generic;
using TechArtPlayground.Water;
using UnityEngine;

[System.Serializable]
public struct EnemySpawnData
{
    [Tooltip("The physical Transform where the enemy will spawn.")]
    public Transform SpawnPoint;

    [Tooltip("The specific enemy prefab to spawn here.")]
    public GameObject EnemyPrefab; 
}

[RequireComponent(typeof(Collider))]
public class EncounterRoomTrigger : MonoBehaviour
{
    [Header("Room Settings")]
    public bool isCleared = false;
    public bool autoClearOnEnemiesDefeated = true;

    [Header("Doors")]
    [Tooltip("Drag all DoorControllers associated with this room here.")]
    [SerializeField] private List<DoorController> roomDoors = new List<DoorController>();
    
    [Header("Encounter Configuration")]
    [SerializeField] private List<EnemySpawnData> enemySpawns = new List<EnemySpawnData>();

    [SerializeField] private OceanWeatherController weatherController;
    
    // Track active enemies to know when the room is cleared
    private readonly List<GameObject> activeEnemies = new List<GameObject>();

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCleared) return;

        if (other.CompareTag("Player"))
        {
            InitiateEncounter();
        }
    }

    private void InitiateEncounter()
    {
        Debug.Log($"[EncounterRoom] Player entered {gameObject.name}. Initiating Combat!");
        
        var playerCtrl = FindAnyObjectByType<PlayerController>(); 
        if (playerCtrl is not null) playerCtrl.StopMovement();

        // 1. Lock the player inside
        foreach (DoorController door in roomDoors)
        {
            if (door != null) door.CloseDoor();
        }
        
        weatherController.TriggerTempest();

        // 2. Switch mode and spawn enemies
        GameModeManager.Instance.SwitchToCombat();
        SpawnEnemies();
    }

    private void SpawnEnemies()
    {
        foreach (EnemySpawnData spawnData in enemySpawns)
        {
            if (spawnData.SpawnPoint is null || spawnData.EnemyPrefab is null)
            {
                Debug.LogWarning($"[EncounterRoom] Invalid spawn data in {gameObject.name}!");
                continue;
            }

            // Instantiate the enemy
            GameObject spawnedEnemy = Instantiate(
                spawnData.EnemyPrefab, 
                spawnData.SpawnPoint.position, 
                spawnData.SpawnPoint.rotation
            );

            activeEnemies.Add(spawnedEnemy);

            // TODO: Subscribe to the enemy's death event here
            spawnedEnemy.GetComponent<HealthComponent>().OnDeath += HandleEnemyDeath;
        }
    }

    /// <summary>
    /// Call this whenever an enemy dies to evaluate if the room is cleared.
    /// </summary>
    public void HandleEnemyDeath(GameObject deadEnemy)
    {
        activeEnemies.Remove(deadEnemy);

        if (autoClearOnEnemiesDefeated && activeEnemies.Count == 0)
        {
            ResolveEncounter();
        }
    }
    
    public void ResolveEncounter()
    {
        isCleared = true;
        Debug.Log($"[EncounterRoom] Room cleared. Returning to Exploration.");
    
        // 1. Unlock the room
        foreach (DoorController door in roomDoors)
        {
            if (door is not null) door.OpenDoor();
        }
        
        weatherController.TriggerCalm();

        // 2. Resume normal gameplay
        GameModeManager.Instance.SwitchToExploration();
    }

    // --- QUALITY OF LIFE: Level Designer Tooling ---
    private void OnDrawGizmosSelected()
    {
        if (enemySpawns == null || enemySpawns.Count == 0) return;

        foreach (EnemySpawnData spawn in enemySpawns)
        {
            if (spawn.SpawnPoint is not null)
            {
                // Draw a red sphere at the spawn location
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                Gizmos.DrawSphere(spawn.SpawnPoint.position, 0.5f);

                // Draw a line connecting the room trigger to the spawn point
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, spawn.SpawnPoint.position);

                // Draw a directional ray to show which way the enemy will face
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(spawn.SpawnPoint.position, spawn.SpawnPoint.forward * 1.5f);
            }
        }
    }
}