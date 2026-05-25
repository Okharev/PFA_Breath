using UnityEngine;

namespace Enemies
{
    public class MortarEnemyAI : BaseTurnAI
    {
        public enum AIState
        {
            Chasing,
            Attacking,
            CoolingDown
        }

        [Header("Mortar AI Settings")] public AIState currentState = AIState.Chasing;

        public Transform target;
        public float moveSpeed = 3.5f;

        [Header("Combat Settings")] public GameObject projectilePrefab;

        public Transform firePoint;
        public int attackCooldownTurns = 2;
        public int projectileCount = 6;
        public float mortarSpreadRadius = 4.5f;
        public int turnsForProjectileToLand = 1;

        private int currentCooldownTurns;
        private bool isAttackQueued;
        private float projectileSplashRadius = 1.5f;

        protected override void Start()
        {
            base.Start(); // Ensure BaseTurnAI registers this entity with TurnManager
            agent.stoppingDistance = mortarSpreadRadius; 

            if (projectilePrefab != null && projectilePrefab.TryGetComponent(out MortarProjectile proj))
            {
                projectileSplashRadius = proj.splashRadius;
            }

            // --- NEW: Target Acquisition ---
            if (target == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) 
                {
                    target = playerObj.transform;
                }
                else 
                {
                    Debug.LogWarning($"[MortarEnemyAI] {gameObject.name} could not find an object tagged 'Player'.");
                }
            }
        }

        // --- PHYSICS & MOVEMENT ---
        private void FixedUpdate()
        {
            if (target == null || !TurnManager.Instance.IsExecuting) return;

            if (currentState == AIState.Chasing)
            {
                agent.SetDestination(target.position);
                if (agent.remainingDistance <= agent.stoppingDistance && !agent.pathPending) return;

                Vector3 targetPathNode = agent.hasPath ? agent.steeringTarget : target.position;
                Vector3 direction = (targetPathNode - rb.position).normalized;
                direction.y = 0;

                rb.MovePosition(rb.position + direction * (moveSpeed * Time.fixedDeltaTime));
            }
        }

        // --- 1. PLANNING PHASE ---
        protected override void OnPlanAction()
        {
            if (target == null) return;

            float distance = Vector3.Distance(transform.position, target.position);
            float effectiveRange = mortarSpreadRadius + projectileSplashRadius;

            if (currentCooldownTurns > 0)
            {
                currentState = AIState.CoolingDown;
                isAttackQueued = false;
            }
            else if (distance <= effectiveRange)
            {
                currentState = AIState.Attacking;
                isAttackQueued = true;
                // Optional: Call your AoE ActionVisualizer here
            }
            else
            {
                currentState = AIState.Chasing;
                isAttackQueued = false;
            }
        }

        // --- 2. EXECUTION PHASE ---
        protected override void OnExecuteAction()
        {
            if (isAttackQueued)
            {
                FireMortarBarrage();
                isAttackQueued = false;
                currentCooldownTurns = attackCooldownTurns;
            }
        }

        // --- 3. END TURN PHASE ---
        protected override void OnEndTurn()
        {
            if (currentCooldownTurns > 0) currentCooldownTurns--;
        }

        private void FireMortarBarrage()
        {
            float angleStep = 360f / projectileCount;
            for (int i = 0; i < projectileCount; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 landingSpot = transform.position +
                                      new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * mortarSpreadRadius;

                GameObject projObj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
                if (projObj.TryGetComponent(out MortarProjectile projectile))
                    projectile.Launch(landingSpot, turnsForProjectileToLand);
            }
        }
    }
}