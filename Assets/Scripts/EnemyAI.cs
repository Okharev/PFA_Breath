using UnityEngine;

/// <summary>
///     Controls enemy AI behavior using a simple Finite State Machine.
///     Respects the TimeTickManager seamlessly via Time.deltaTime and FixedUpdate.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState
    {
        Chasing,
        Attacking
    }

    [Header("AI Settings")]
    [Tooltip("The state machine's current phase.")]
    public AIState currentState = AIState.Chasing;
    public Transform target;
    
    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public float rotationSpeed = 10f;
    
    [Header("Combat")]
    public float attackRange = 8f;
    [Tooltip("Minimum real-game seconds between shots.")]
    public float fireCooldown = 1.5f;
    public GameObject projectilePrefab;
    public Transform firePoint;

    private Rigidbody rb;
    private float shootTimer;
    
    private Vector3 lockedAimPosition;
    private bool isAimLocked = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Cache the target reference. Avoid using GameObject.Find() in Update!
        if (target is null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player) target = player.transform;
            else Debug.LogWarning("EnemyAI: No Player target found!");
        }
    }

    [Header("Visuals")]
    public ActionVisualizer aimVisualizer;

    private void Update()
    {
        if (target is null) return;

        // 1. Cooldown Management
        if (shootTimer > 0f) shootTimer -= Time.deltaTime;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget <= attackRange)
        {
            currentState = AIState.Attacking;
            
            // 2. Lock Aim ONLY when time is paused.
            // This forces the enemy to wait until you stop moving to draw their laser,
            // guaranteeing you have a chance to react before they fire.
            if (shootTimer <= 0f && !isAimLocked && !TimeTickManager.Instance.IsTimeFlowing())
            {
                lockedAimPosition = new Vector3(target.position.x, firePoint.position.y, target.position.z);
                isAimLocked = true;
            }

            // 3. Draw Telegraph Line
            if (isAimLocked && aimVisualizer is not null)
            {
                aimVisualizer.DrawIntent(firePoint.position, lockedAimPosition);
            }
            else if (aimVisualizer is not null)
            {
                // The line is hidden while the enemy is reloading
                aimVisualizer.Hide(); 
            }
            
            // 4. Fire Weapon
            if (isAimLocked && TimeTickManager.Instance.IsTimeFlowing())
            {
                Shoot(lockedAimPosition);
            }
        }
        else
        {
            currentState = AIState.Chasing;
            
            isAimLocked = false; 
            if (aimVisualizer is not null) aimVisualizer.Hide();
        }
    }
    // Notice we now pass the locked target position into the Shoot method
    private void Shoot(Vector3 targetPos)
    {
        // Reset timers and break the lock so they have to re-aim next time
        shootTimer = fireCooldown;
        isAimLocked = false; 

        // Calculate direction towards the locked position
        Vector3 shootDirection = (targetPos - firePoint.position).normalized;

        Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(shootDirection));
        
        // Hide the laser exactly as the bullet is fired
        aimVisualizer?.Hide();
    }

    private void FixedUpdate()
    {
        if (target is null) return;

        // FixedUpdate stops firing when Time.timeScale == 0, 
        // locking the physics body perfectly in place.
        Vector3 directionToTarget = (target.position - rb.position).normalized;
        directionToTarget.y = 0; // Keep movement locked to the XZ plane

        switch (currentState)
        {
            case AIState.Chasing:
                HandleMovement(directionToTarget);
                break;

            case AIState.Attacking:
                HandleRotation(directionToTarget); // Just look at player while attacking
                break;
        }
    }

    private void HandleMovement(Vector3 direction)
    {
        // Move towards target
        Vector3 newPos = rb.position + direction * (moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);

        HandleRotation(direction);
    }

    private void HandleRotation(Vector3 direction)
    {
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }
    }

    private void Shoot()
    {
        shootTimer = fireCooldown;

        // Aim strictly towards the player's core/feet based on firePoint height
        Vector3 targetPoint = new Vector3(target.position.x, firePoint.position.y, target.position.z);
        Vector3 shootDirection = (targetPoint - firePoint.position).normalized;

        Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(shootDirection));
        
        // Note: The AI DOES NOT call TimeTickManager.Instance.TriggerActionTick().
        // In a Chronosphere clone, AI actions react to the player's time flow, 
        // they don't instigate it.
    }
}