using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles player shooting mechanics, integrated with the TimeTick and Oxygen systems.
/// </summary>
[RequireComponent(typeof(PlayerOxygen))]
public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting Settings")]
    public int shootingOxygenCost = 15;
    public float actionTimeCost = 0.5f;
    
    [Header("References")]
    [Tooltip("The prefab to spawn when shooting.")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    private PlayerOxygen oxygen;

    private void Start()
    {
        oxygen = GetComponent<PlayerOxygen>();
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        // Only allow shooting if time is paused/ready for a new action
        if (!TimeTickManager.Instance.IsTimeFlowing())
        {
            // Example input: Right Click to shoot
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                AttemptShot();
            }
        }
    }

    private void AttemptShot()
    {
        // 1. GATEKEEPER: Ask the Oxygen system if we can afford the shot
        if (oxygen.TryConsume(shootingOxygenCost))
        {
            // 2. Execute the shot
            FireProjectile();

            // 3. Trigger the time flow
            TimeTickManager.Instance.TriggerActionTick(actionTimeCost);
        }
        else
        {
            Debug.Log("Cannot shoot: Insufficient Oxygen.");
            // Optional: Trigger a "dry fire" sound effect here
        }
    }

    private void FireProjectile()
    {
        if (projectilePrefab is not null && firePoint is not null)
        {
            // Instantiate the projectile (Consider using an Object Pool here for production code)
            Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        }
    }
}