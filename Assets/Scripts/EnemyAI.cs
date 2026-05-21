using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody), typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState
    {
        Chasing,
        Attacking
    }

    [Header("AI Settings")] public AIState currentState = AIState.Chasing;

    public Transform target;

    // --- NEW: LayerMask for optimized physics queries ---
    [Header("Targeting & LoS")] [Tooltip("Include layers that block vision: Player, Walls, Environment")]
    public LayerMask lineOfSightMask;

    [Header("Movement")] public float moveSpeed = 3.5f;

    public float rotationSpeed = 10f;

    [Header("Combat")] public float attackRange = 8f;

    public int attackCooldownTurns = 2;
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Visuals")] public ActionVisualizer aimVisualizer;

    private NavMeshAgent agent;
    private int currentCooldownTurns;

    private bool isAimLocked;
    private Vector3 lockedAimPosition;
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        agent.updatePosition = false;
        agent.updateRotation = false;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player) target = player.transform;
            else Debug.LogWarning("EnemyAI: No Player target found!");
        }
    }

    private void Update()
    {
        if (target is null) return;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        // --- NEW: State Transition Logic requires both Distance AND Line of Sight ---
        if (distanceToTarget <= attackRange && HasLineOfSight(distanceToTarget))
        {
            currentState = AIState.Attacking;

            if (currentCooldownTurns <= 0 && !isAimLocked && !TurnManager.Instance.IsExecuting)
            {
                lockedAimPosition = new Vector3(target.position.x, firePoint.position.y, target.position.z);
                isAimLocked = true;
            }

            if (isAimLocked && aimVisualizer is not null)
                aimVisualizer.DrawIntent(firePoint.position, lockedAimPosition, ActionVisualizer.IntentType.Shooting);
            else
                aimVisualizer?.Hide();

            if (isAimLocked && TurnManager.Instance.IsExecuting)
                Shoot(lockedAimPosition);
        }
        else
        {
            // If the player is out of range OR out of sight, keep chasing.
            currentState = AIState.Chasing;
            isAimLocked = false;
            aimVisualizer?.Hide();
        }

        agent.nextPosition = rb.position;
    }

    private void FixedUpdate()
    {
        if (target is null || !TurnManager.Instance.IsExecuting) return;

        switch (currentState)
        {
            case AIState.Chasing:
                agent.SetDestination(target.position);

                // Note: Ensure steeringTarget is valid, otherwise fallback to target position
                Vector3 targetPathNode = agent.hasPath ? agent.steeringTarget : target.position;
                Vector3 directionToPath = (targetPathNode - rb.position).normalized;
                directionToPath.y = 0;

                HandleMovement(directionToPath);
                break;

            case AIState.Attacking:
                Vector3 lookDir = (target.position - rb.position).normalized;
                lookDir.y = 0;
                HandleRotation(lookDir);
                break;
        }
    }

    private void OnEnable()
    {
        TurnManager.OnTurnTicked += HandleTurnTicked;
    }

    private void OnDisable()
    {
        TurnManager.OnTurnTicked -= HandleTurnTicked;
    }

    private void HandleTurnTicked(int currentTurn)
    {
        if (currentCooldownTurns > 0) currentCooldownTurns--;
    }

    // --- NEW: The Line of Sight Method ---
    private bool HasLineOfSight(float distanceToTarget)
    {
        // 1. Aim at the center of the player's body (assuming a standard ~2 unit tall character).
        // Adjust the "Vector3.up * 1f" if your player is taller or shorter.
        Vector3 targetCenter = target.position + Vector3.up * 1f;
        Vector3 directionToTarget = (targetCenter - firePoint.position).normalized;

        // --- VISUAL DEBUGGING ---
        // This will draw a red line in your Scene View (make sure Gizmos are enabled)
        // so you can literally see where the ray is going and if it's clipping the floor.
        Debug.DrawRay(firePoint.position, directionToTarget * distanceToTarget, Color.red);

        // We add a tiny bit of extra length (0.5f) to ensure the ray fully reaches inside the player's collider.
        if (Physics.Raycast(firePoint.position, directionToTarget, out RaycastHit hit, distanceToTarget + 0.5f,
                lineOfSightMask))
        {
            // 2. Safest way to check for the player: Use CompareTag or check the root object.
            // This works even if the collider is on a nested child object.
            if (hit.collider.CompareTag("Player") || hit.transform.root == target.root) return true;

            // If LoS fails, this tells us exactly what object blocked the view!
            Debug.Log($"Enemy {gameObject.name} LoS blocked by: {hit.transform.name}");
        }

        return false;
    }

    private void Shoot(Vector3 targetPos)
    {
        currentCooldownTurns = attackCooldownTurns;
        isAimLocked = false;

        Vector3 shootDirection = (targetPos - firePoint.position).normalized;
        Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(shootDirection));

        aimVisualizer?.Hide();
    }

    private void HandleMovement(Vector3 direction)
    {
        Vector3 newPos = rb.position + direction * (moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
        HandleRotation(direction);
    }

    private void HandleRotation(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }
    }
}