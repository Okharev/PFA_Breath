using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerOxygen))]
public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting Settings")] 
    public int shootingOxygenCost = 15;
    public int actionTurnCost = 1;
    public float maxShootDistance = 15f;
    
    [Header("Weapon Spread")]
    [Tooltip("Maximum angle of deviation in degrees (0 = perfect accuracy).")]
    [Range(0f, 45f)]
    public float spreadAngle = 2.5f;

    [Header("Aiming Settings")]
    [Tooltip("Must match the Floor Mask in PlayerController to prevent aiming desync.")]
    public LayerMask floorMask;

    [Header("References")] 
    public GameObject projectilePrefab;
    public Transform firePoint;
    public ActionVisualizer actionVisualizer; 
    
    private Camera mainCam;
    private PlayerOxygen oxygen;

    private void Start()
    {
        oxygen = GetComponent<PlayerOxygen>();
        mainCam = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null || mainCam == null) return;

        // Gatekeeper: Only evaluate when awaiting input
        if (!TurnManager.Instance.IsExecuting)
        {
            Vector3? targetPos = GetAimTarget();

            if (targetPos.HasValue)
            {
                // We draw the preview every frame while aiming
                actionVisualizer.DrawIntent(firePoint.position, targetPos.Value, ActionVisualizer.IntentType.Shooting, spreadAngle);
                
                // Commit to the shot
                if (Mouse.current.rightButton.wasPressedThisFrame) 
                {
                    AttemptShot(targetPos.Value);
                }
            }
        }
        else
        {
            // Only hide if we are currently executing
            actionVisualizer?.Hide(); 
        }
    }

    private Vector3? GetAimTarget()
    {
        Ray ray = mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());

        // Raycast against the floor to match locomotion aiming.
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, floorMask))
        {
            Vector3 target = new Vector3(hit.point.x, firePoint.position.y, hit.point.z);
            Vector3 direction = target - firePoint.position;

            // Clamp to max distance
            if (direction.magnitude > maxShootDistance)
            {
                target = firePoint.position + direction.normalized * maxShootDistance;
            }

            return target;
        }
        return null;
    }

    private void AttemptShot(Vector3 targetPosition)
    {
        if (oxygen.TryConsume(shootingOxygenCost))
        {
            actionVisualizer?.Hide(); 
            FireProjectile(targetPosition);
            TurnManager.Instance.ExecuteTurns(actionTurnCost);
        }
        else
        {
            Debug.Log("Cannot shoot: Insufficient Oxygen.");
        }
    }

    private void FireProjectile(Vector3 targetPosition)
    {
        if (projectilePrefab != null && firePoint != null)
        {
            // Calculate the pure, 100% accurate trajectory
            Vector3 baseShootDirection = (targetPosition - firePoint.position).normalized;
            
            // --- THE SPREAD LOGIC ---
            // Generate a random angle between negative and positive spreadAngle
            float randomYaw = Random.Range(-spreadAngle, spreadAngle);
            
            // Create a rotation around the Y axis
            Quaternion spreadRotation = Quaternion.Euler(0f, randomYaw, 0f);
            
            // Multiply the Quaternion by the Vector3 to rotate the vector
            Vector3 finalShootDirection = spreadRotation * baseShootDirection;
            
            // Only instantiate if we have a valid direction
            if (finalShootDirection.sqrMagnitude > 0.01f)
            {
                Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(finalShootDirection));
            }
        }
    }
}