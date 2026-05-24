using Skills;
using UnityEngine;
using UnityEngine.AI;

public interface IPlayerMovementState
{
    void EnterState();
    void ExitState();
    void Update();
    void FixedUpdate();
    void StartMovement(Vector3 target);
    void StopMovement();
}

public class ExplorationMovementState : IPlayerMovementState
{
    private readonly PlayerController context;
    private readonly NavMeshAgent agent;
    private readonly Rigidbody rb;

    public ExplorationMovementState(PlayerController context, NavMeshAgent agent, Rigidbody rb)
    {
        this.context = context;
        this.agent = agent;
        this.rb = rb;
    }

    public void EnterState()
    {
        // 1. Put the Rigidbody into a complete coma
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None; // CRITICAL: Prevents physics stutter

        // 2. Hand the keys to the NavMeshAgent
        agent.enabled = true;
        agent.updatePosition = true; // Agent directly moves the Transform
        agent.updateRotation = false; // We still handle rotation manually for smoothness
        
        // Snappy movement settings
        // TODO This is fked needs to be dynamic
        agent.speed = 8f;
        agent.acceleration = 100f;
        agent.stoppingDistance = 0.1f;
        agent.autoBraking = true;
    }

    public void ExitState()
    {
        if (agent.enabled)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // Wake the Rigidbody back up for Combat State
        rb.interpolation = RigidbodyInterpolation.Interpolate; 
    }

    public void Update()
    {
        // Smooth rotational logic looking at the next path corner
        if (context.IsMoving && agent.enabled && agent.hasPath)
        {
            Vector3 directionToTarget = agent.steeringTarget - rb.position;
            if (directionToTarget.sqrMagnitude > 0.01f)
            {
                context.LookAtTarget(agent.steeringTarget);
            }
            
            // Check if we arrived at the destination
            if (agent.remainingDistance <= agent.stoppingDistance && !agent.pathPending)
            {
                context.StopMovement();
            }
        }
    }

    public void FixedUpdate()
    {
        // Completely empty. The Agent handles positional movement in Update.
    }

    public void StartMovement(Vector3 target)
    {
        // If the player isn't perfectly snapped to the NavMesh, this fails silently!
        if (agent.enabled && agent.isOnNavMesh) // <--- If this is false once, input drops forever
        {
            context.IsMoving = true;
            agent.isStopped = false;
            agent.SetDestination(target); 
        }
    }

    public void StopMovement()
    {
        context.IsMoving = false;
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }
}

public class CombatMovementState : IPlayerMovementState
{
    private readonly PlayerController context;
    private readonly Rigidbody rb;
    private Vector3 targetPosition;

    public CombatMovementState(PlayerController context, Rigidbody rb)
    {
        this.context = context;
        this.rb = rb;
    }

    public void EnterState()
    {
        context.IsMoving = false;
        // Start with interpolation OFF so we can aim smoothly while time is paused
        rb.interpolation = RigidbodyInterpolation.None; 
    }

    public void ExitState()
    {
        StopMovement();
        rb.interpolation = RigidbodyInterpolation.None;
    }

    public void Update() 
    {
        // --- THE FIX: Re-introduced your dynamic interpolation toggle ---
        if (!TurnManager.Instance.IsExecuting)
        {
            if (rb.interpolation != RigidbodyInterpolation.None)
                rb.interpolation = RigidbodyInterpolation.None;
        }
        else
        {
            if (rb.interpolation != RigidbodyInterpolation.Interpolate)
                rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    public void FixedUpdate()
    {
        if (context.IsMoving && TurnManager.Instance.IsExecuting)
        {
            Vector3 direction = (targetPosition - rb.position).normalized;
            
            //  Pull the dynamically calculated math from PlayerStats via the property
            float currentSpeed = context.IsDashing ? (context.CombatMoveSpeed * context.dashSpeedMultiplier) : context.CombatMoveSpeed;
            
            float distanceThisFrame = currentSpeed * Time.fixedDeltaTime;

            if (rb.SweepTest(direction, out RaycastHit hit, distanceThisFrame + 0.05f))
            {
                Vector3 safePos = rb.position + direction * Mathf.Max(0, hit.distance - 0.05f);
                rb.MovePosition(safePos);
                context.StopMovement(); 
            }
            else
            {
                Vector3 newPos = Vector3.MoveTowards(rb.position, targetPosition, distanceThisFrame);
                rb.MovePosition(newPos);
            }

            if (Vector3.Distance(rb.position, targetPosition) < 0.05f)
            {
                context.StopMovement(); 
            }
        }
    }

    public void StartMovement(Vector3 target)
    {
        context.ResetDashState();
        targetPosition = target;
        context.IsMoving = true;
    }

    public void StopMovement()
    {
        context.ResetDashState();
        context.IsMoving = false;
    }
}

[RequireComponent(typeof(Rigidbody), typeof(NavMeshAgent), typeof(PlayerStats))] // ADDED PlayerStats requirement
public class PlayerController : MonoBehaviour
{
    // REMOVED: public float moveSpeed = 5f;
    // REMOVED: public float explorationSpeed = 6f;

    [Header("Movement Settings")]
    public float rotationSpeed = 30f;
    
    [Header("Dash Settings")] 
    public float dashSpeedMultiplier = 3f;
    public string dashLayerName = "Dashing";
    
    // Core References
    private NavMeshAgent agent;
    private Rigidbody rb;
    private PlayerStats playerStats; // NEW

    // State Tracking
    private CombatMovementState combatState;
    private IPlayerMovementState currentState;
    private ExplorationMovementState explorationState;
    private int dashLayerIndex;
    private int originalLayer;

    public bool IsInvincible { get; private set; }
    public bool IsMoving { get; set; }
    public bool IsDashing { get; private set; }

    // --- NEW: DYNAMIC STAT PROPERTIES ---
    // These pull instantly from the O(1) dictionary, keeping performance fast while ensuring 100% accuracy with the Skill Tree
    public float CombatMoveSpeed => playerStats.GetStatValue(StatType.MovementSpeed);
    public float ExplorationMoveSpeed => playerStats.GetStatValue(StatType.MovementSpeed) * 1.2f; // E.g., 20% faster out of combat

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
        playerStats = GetComponent<PlayerStats>(); // CACHE THE COMPONENT

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        dashLayerIndex = LayerMask.NameToLayer(dashLayerName);

        explorationState = new ExplorationMovementState(this, agent, rb);        
        combatState = new CombatMovementState(this, rb);

        GameModeManager.OnGameModeChanged += HandleGameModeChanged;

        if (GameModeManager.Instance != null)
        {
            HandleGameModeChanged(GameModeManager.Instance.CurrentMode);
        }
        else
        {
            ChangeState(explorationState); 
        }
    }

    private void Update()
    {
        currentState?.Update();
    }

    private void FixedUpdate()
    {
        currentState?.FixedUpdate();
    }

    private void OnDestroy()
    {
        GameModeManager.OnGameModeChanged -= HandleGameModeChanged;
    }

    private void HandleGameModeChanged(GameMode mode)
    {
        if (mode == GameMode.Exploration)
            ChangeState(explorationState);
        else
            ChangeState(combatState);
    }

    private void ChangeState(IPlayerMovementState newState)
    {
        currentState?.ExitState();
        currentState = newState;
        currentState?.EnterState();
    }

    public void StartMovement(Vector3 target)
    {
        currentState?.StartMovement(target);
    }

    public void StopMovement()
    {
        currentState?.StopMovement();
    }

    public void TeleportTo(Vector3 newPosition)
    {
        StopMovement();
        if (agent.enabled && agent.isOnNavMesh) agent.Warp(newPosition);

        transform.position = newPosition;
        rb.position = newPosition;
        Physics.SyncTransforms();
    }

    public void StartDash(Vector3 target)
    {
        // Dash logic remains shared, but movement relies on the active state
        IsMoving = true;

        if (!IsDashing)
        {
            originalLayer = gameObject.layer;
            if (dashLayerIndex != -1) gameObject.layer = dashLayerIndex;
        }

        IsDashing = true;
        IsInvincible = true;
        currentState?.StartMovement(target);
    }

    public void ResetDashState()
    {
        if (IsDashing)
        {
            if (dashLayerIndex != -1) gameObject.layer = originalLayer;
            IsDashing = false;
            IsInvincible = false;
        }
    }

    public void LookAtTarget(Vector3 targetPos)
    {
        Vector3 lookDirection = targetPos - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = rotationSpeed <= 0f
                ? targetRotation
                : Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.unscaledDeltaTime);
        }
    }
}