using UnityEngine;

namespace Enemies
{
    public class EnemyAI : BaseTurnAI
    {
        public enum AIState
        {
            Chasing,
            Attacking,
            CoolingDown
        }

        [Header("Standard AI Settings")] public AIState currentState = AIState.Chasing;

        public Transform target;
        public LayerMask lineOfSightMask;	
        public float moveSpeed = 3.5f;
        public float rotationSpeed = 10f;

        [Header("Combat Settings")] public float attackRange = 8f;

        public int attackCooldownTurns = 2;
        public GameObject projectilePrefab;
        public Transform firePoint;
        public ActionVisualizer aimVisualizer;

        private int currentCooldownTurns;
        private bool isAttackQueued;
        private Vector3 queuedAimPosition;

        protected override void Start()
        {
            base.Start(); // Ensure BaseTurnAI registers this entity with TurnManager

            // --- Target Acquisition ---
            if (target == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) 
                {
                    target = playerObj.transform;
                }
                else 
                {
                    Debug.LogWarning($"[EnemyAI] {gameObject.name} could not find an object tagged 'Player'.");
                }
            }
        }
        
        // --- PHYSICS & MOVEMENT ---
        private void FixedUpdate()
        {
            if (target is null || !TurnManager.Instance.IsExecuting) return;

            if (currentState == AIState.Chasing)
            {
                agent.SetDestination(target.position);
                Vector3 targetPathNode = agent.hasPath ? agent.steeringTarget : target.position;
                Vector3 direction = (targetPathNode - rb.position).normalized;
                direction.y = 0;

                rb.MovePosition(rb.position + direction * (moveSpeed * Time.fixedDeltaTime));
                HandleRotation(direction);
            }
            else if (currentState == AIState.Attacking || currentState == AIState.CoolingDown)
            {
                Vector3 lookDir = (target.position - rb.position).normalized;
                lookDir.y = 0;
                HandleRotation(lookDir);
            }
        }


        // --- 1. PLANNING PHASE ---
        protected override void OnPlanAction()
        {
            if (target is null) return;

            float distance = Vector3.Distance(transform.position, target.position);

            if (currentCooldownTurns > 0)
            {
                currentState = AIState.CoolingDown;
                CancelQueuedAttack();
            }
            else if (distance <= attackRange && HasLineOfSight(distance))
            {
                currentState = AIState.Attacking;
                if (!isAttackQueued)
                {
                    isAttackQueued = true;
                    queuedAimPosition = new Vector3(target.position.x, firePoint.position.y, target.position.z);
                    aimVisualizer?.DrawIntent(firePoint.position, queuedAimPosition,
                        ActionVisualizer.IntentType.Shooting);
                }
            }
            else
            {
                currentState = AIState.Chasing;
                CancelQueuedAttack();
            }
        }

        // --- 2. EXECUTION PHASE ---
        protected override void OnExecuteAction()
        {
            if (isAttackQueued)
            {
                Vector3 shootDir = (queuedAimPosition - firePoint.position).normalized;
                Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(shootDir));

                currentCooldownTurns = attackCooldownTurns;
                CancelQueuedAttack();
            }
        }

        // --- 3. END TURN PHASE ---
        protected override void OnEndTurn()
        {
            if (currentCooldownTurns > 0) currentCooldownTurns--;
        }

        private void CancelQueuedAttack()
        {
            isAttackQueued = false;
            aimVisualizer?.Hide();
        }

        private void HandleRotation(Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));
            }
        }

        private bool HasLineOfSight(float distance)
        {
            Vector3 targetCenter = target.position + Vector3.up * 1f;
            Vector3 dir = (targetCenter - firePoint.position).normalized;
            if (Physics.Raycast(firePoint.position, dir, out RaycastHit hit, distance + 0.5f, lineOfSightMask))
                return hit.collider.CompareTag("Player") || hit.transform.root == target.root;
            return false;
        }
    }
}