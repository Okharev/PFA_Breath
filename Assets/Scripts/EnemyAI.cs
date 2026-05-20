using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    public enum AIState { Chasing, Attacking }

    [Header("AI Settings")] 
    public AIState currentState = AIState.Chasing;
    public Transform target;

    [Header("Movement")] 
    public float moveSpeed = 3.5f;
    public float rotationSpeed = 10f;

    [Header("Combat")] 
    public float attackRange = 8f;
    public int attackCooldownTurns = 2; 
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Visuals")] 
    public ActionVisualizer aimVisualizer;

    private bool isAimLocked;
    private Vector3 lockedAimPosition;
    private Rigidbody rb;
    private int currentCooldownTurns = 0;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        // --- KINEMATIC SETUP ---
        rb.isKinematic = false; 
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate; 
        // -----------------------

        if (target is null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player) target = player.transform;
            else Debug.LogWarning("EnemyAI: No Player target found!");
        }
    }

    private void OnEnable() => TurnManager.OnTurnTicked += HandleTurnTicked;
    private void OnDisable() => TurnManager.OnTurnTicked -= HandleTurnTicked;

    private void HandleTurnTicked(int currentTurn)
    {
        if (currentCooldownTurns > 0) currentCooldownTurns--;
    }

    private void Update()
    {
        if (target is null) return;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget <= attackRange)
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
            {
                aimVisualizer?.Hide();
            }

            if (isAimLocked && TurnManager.Instance.IsExecuting) 
                Shoot(lockedAimPosition);
        }
        else
        {
            currentState = AIState.Chasing;
            isAimLocked = false;
            if (aimVisualizer is not null) aimVisualizer.Hide();
        }
    }

    private void FixedUpdate()
    {
        if (target is null || !TurnManager.Instance.IsExecuting) return;

        Vector3 directionToTarget = (target.position - rb.position).normalized;
        directionToTarget.y = 0;

        switch (currentState)
        {
            case AIState.Chasing:
                HandleMovement(directionToTarget);
                break;
            case AIState.Attacking:
                HandleRotation(directionToTarget);
                break;
        }
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
        // rb.MovePosition works identically on Kinematic bodies, but bypasses collision bouncing
        Vector3 newPos = rb.position + direction * (moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
        HandleRotation(direction);
    }

    private void HandleRotation(Vector3 direction)
    {
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            // rb.MoveRotation ensures rotation interpolates cleanly
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }
    }
}