using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")] 
    public float moveSpeed = 5f;
    public float rotationSpeed = 30f;

    [Header("Dash Settings")]
    public float dashSpeedMultiplier = 3f;
    [Tooltip("The name of the layer the player switches to while dashing to dodge bullets.")]
    public string dashLayerName = "Dashing";

    public bool IsInvincible { get; private set; }

    private bool isMoving;
    private bool isDashing;
    private Rigidbody rb;
    private Vector3 targetPosition;
    
    // --- NEW: Layer Tracking ---
    private int originalLayer;
    private int dashLayerIndex;

    private const RigidbodyInterpolation DefaultInterpolation = RigidbodyInterpolation.Interpolate;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = DefaultInterpolation; 
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Cache the layer index for performance
        dashLayerIndex = LayerMask.NameToLayer(dashLayerName);
        if (dashLayerIndex == -1)
        {
            Debug.LogWarning($"[PlayerController] Layer '{dashLayerName}' does not exist! Please create it in the top right of the Unity Editor.");
        }
    }
    
    public void StartMovement(Vector3 target)
    {
        // BUG FIX: If we were dashing and got interrupted by a normal move, 
        // we must revert the layer BEFORE starting the new movement.
        ResetDashState(); 
        
        targetPosition = target;
        isMoving = true;
    }
    
    public void StartDash(Vector3 target)
    {
        targetPosition = target;
        isMoving = true;
        
        if (!isDashing) 
        {
            originalLayer = gameObject.layer;
            if (dashLayerIndex != -1) gameObject.layer = dashLayerIndex;
        }

        isDashing = true;
        IsInvincible = true; 
    }

    /// <summary>
    /// Safely teleports the player, cancelling any active movement or dashes
    /// and ensuring the Rigidbody physics don't glitch out.
    /// </summary>
    public void TeleportTo(Vector3 newPosition)
    {
        // 1. Cancel any active walking or dashing states
        StopMovement();

        // 2. Snap both the Transform and the Kinematic Rigidbody
        transform.position = newPosition;
        rb.position = newPosition;
        
        // 3. Force the physics engine to update immediately
        Physics.SyncTransforms();
    }
    
    private void Update()
    {
        if (!TurnManager.Instance.IsExecuting)
        {
            if (rb.interpolation != RigidbodyInterpolation.None)
                rb.interpolation = RigidbodyInterpolation.None;
        }
        else
        {
            if (rb.interpolation != DefaultInterpolation)
                rb.interpolation = DefaultInterpolation;
        }
    }

    public void LookAtTarget(Vector3 targetPos)
    {
        Vector3 lookDirection = targetPos - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            if (rotationSpeed <= 0f) 
                transform.rotation = targetRotation;
            else 
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.unscaledDeltaTime);
        }
    }

    private void FixedUpdate()
    {
        if (isMoving && TurnManager.Instance.IsExecuting)
        {
            Vector3 direction = (targetPosition - rb.position).normalized;
            float currentSpeed = isDashing ? (moveSpeed * dashSpeedMultiplier) : moveSpeed;
            float distanceThisFrame = currentSpeed * Time.fixedDeltaTime;

            if (rb.SweepTest(direction, out RaycastHit hit, distanceThisFrame + 0.05f))
            {
                Vector3 safePos = rb.position + direction * Mathf.Max(0, hit.distance - 0.05f);
                rb.MovePosition(safePos);
                StopMovement(); 
            }
            else
            {
                Vector3 newPos = Vector3.MoveTowards(rb.position, targetPosition, distanceThisFrame);
                rb.MovePosition(newPos);
            }

            if (Vector3.Distance(rb.position, targetPosition) < 0.05f)
            {
                StopMovement(); 
            }
        }
    }
    
    private void ResetDashState()
    {
        if (isDashing)
        {
            if (dashLayerIndex != -1)
            {
                gameObject.layer = originalLayer;
            }
            isDashing = false;
            IsInvincible = false;
        }
    }

    private void StopMovement()
    {
        // BUG FIX: Use the centralized cleanup
        ResetDashState(); 

        isMoving = false;
        rb.interpolation = RigidbodyInterpolation.None;
    }
}