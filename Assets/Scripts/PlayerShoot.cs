using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerOxygen))]
public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting Settings")] 
    public int shootingOxygenCost = 15;
    public int actionTurnCost = 1; 
    public float maxShootDistance = 15f;

    [Header("References")] 
    public GameObject projectilePrefab;
    public Transform firePoint;
    public ActionVisualizer actionVisualizer; // Inject the visualizer

    private PlayerOxygen oxygen;
    private Camera mainCam;

    private void Start()
    {
        oxygen = GetComponent<PlayerOxygen>();
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        // Only allow aiming/shooting if turns are not currently executing
        if (!TurnManager.Instance.IsExecuting)
        {
            HandleAimingPreview();

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                AttemptShot();
            }
        }
        else
        {
            actionVisualizer.Hide(); // Ensure it hides during turn execution
        }
    }

    private void HandleAimingPreview()
    {
        // Simple Raycast to mouse position for aiming
        Ray ray = mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 target = new Vector3(hit.point.x, firePoint.position.y, hit.point.z);
            Vector3 direction = target - firePoint.position;

            // Clamp line to max shooting distance
            if (direction.magnitude > maxShootDistance)
            {
                target = firePoint.position + (direction.normalized * maxShootDistance);
            }

            // Draw the shader-based shooting line
            actionVisualizer.DrawIntent(firePoint.position, target, ActionVisualizer.IntentType.Shooting);
        }
    }

    private void AttemptShot()
    {
        if (oxygen.TryConsume(shootingOxygenCost))
        {
            actionVisualizer.Hide(); // Hide upon firing
            FireProjectile();
            TurnManager.Instance.ExecuteTurns(actionTurnCost);
        }
        else
        {
            Debug.Log("Cannot shoot: Insufficient Oxygen.");
        }
    }

    private void FireProjectile()
    {
        if (projectilePrefab != null && firePoint != null)
        {
            Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        }
    }
}