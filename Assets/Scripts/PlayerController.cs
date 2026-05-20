using UnityEngine;

/// <summary>
/// Handles the physical execution of movement and rotation.
/// It receives commands from the PlayerAbilityController and executes them safely
/// using the Rigidbody during the TurnManager's execution phase.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Action Settings")] 
    public float moveSpeed = 5f;
    public float rotationSpeed = 30f;

    // State Tracking
    private bool isMoving;
    private Rigidbody rb;
    private Vector3 targetPosition;
    
    // Cache the default interpolation to prevent visual stuttering when time pauses/resumes
    private RigidbodyInterpolation defaultInterpolation = RigidbodyInterpolation.Interpolate;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        // --- STRICT KINEMATIC SETUP ---
        // We use kinematic rigidbodies so we have absolute mathematical control over
        // the grid/tactical movement, while still respecting collision via SweepTest.
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = defaultInterpolation; 
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    /// <summary>
    /// Public API called by the MovementAbility to queue a movement execution.
    /// </summary>
    public void StartMovement(Vector3 target)
    {
        targetPosition = target;
        isMoving = true;
        // You can delete the interpolation logic that was here!
    }
    
    private void Update()
    {
        // CRITICAL FIX: Rigidbody Visual Desync
        // When time is paused (Planning Phase), we MUST disable interpolation.
        // Otherwise, modifying transform.rotation for aiming will not visually update.
        if (!TurnManager.Instance.IsExecuting)
        {
            if (rb.interpolation != RigidbodyInterpolation.None)
            {
                rb.interpolation = RigidbodyInterpolation.None;
            }
        }
        else
        {
            // Re-enable interpolation during the Execution Phase so movement is smooth
            if (rb.interpolation != defaultInterpolation)
            {
                rb.interpolation = defaultInterpolation;
            }
        }
    }

    /// <summary>
    /// Handles rotating the player model globally. 
    /// Can be called by aiming abilities to make the character face the mouse.
    /// </summary>
    public void LookAtTarget(Vector3 targetPos)
    {
        Vector3 lookDirection = targetPos - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            
            // Instant snap vs smooth rotation
            if (rotationSpeed <= 0f) 
                transform.rotation = targetRotation;
            else 
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.unscaledDeltaTime);
        }
    }

    private void FixedUpdate()
    {
        // --- EXECUTION PHASE MOVEMENT ---
        // Only move the Rigidbody if we have a target AND the global time is flowing.
        if (isMoving && TurnManager.Instance.IsExecuting)
        {
            Vector3 direction = (targetPosition - rb.position).normalized;
            float distanceThisFrame = moveSpeed * Time.fixedDeltaTime;

            // SweepTest projects the Rigidbody forward to check for walls before moving.
            if (rb.SweepTest(direction, out RaycastHit hit, distanceThisFrame + 0.05f))
            {
                // Wall detected! Move right up to the wall and stop.
                Vector3 safePos = rb.position + direction * Mathf.Max(0, hit.distance - 0.05f);
                rb.MovePosition(safePos);
                isMoving = false;
            }
            else
            {
                // Path is clear. Move normally.
                Vector3 newPos = Vector3.MoveTowards(rb.position, targetPosition, distanceThisFrame);
                rb.MovePosition(newPos);
            }

            // Snap to grid/target if we are close enough
            if (Vector3.Distance(rb.position, targetPosition) < 0.05f)
            {
                isMoving = false;
                
                // Turn off interpolation when stopped to prevent sliding visuals when time pauses
                rb.interpolation = RigidbodyInterpolation.None;
            }
        }
    }
}