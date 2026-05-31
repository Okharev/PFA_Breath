using UnityEngine;
using UnityEngine.AI;

namespace Ability.NewAbilitySystem
{
    public interface IEnemyState
    {
        void Enter(EnemyAIController enemy);

        /// <summary>
        ///     Evaluates logic during the paused planning phase.
        /// </summary>
        void PlanAction(EnemyAIController enemy);

        /// <summary>
        ///     Executes the physical action when the turn begins.
        /// </summary>
        void ExecuteAction(EnemyAIController enemy);

        void Exit(EnemyAIController enemy);
    }

    public enum EnemyStartingState
    {
        Chase,
        Flee,
        Attack
    }

    [RequireComponent(typeof(NavMeshAgent), typeof(AbilityController))]
    public class EnemyAIController : MonoBehaviour, ITurnEntity
    {
        [field: Header("Targeting & Physics")]
        [field: SerializeField]
        public Transform Target { get; private set; }

        [field: SerializeField] public AbilityData ShootAbility { get; private set; }
        [field: SerializeField] public float AttackRange { get; private set; } = 15f;
        [field: SerializeField] public LayerMask SightObstacles { get; private set; }
        [field: SerializeField] public Transform FirePoint;

        [field: Header("Behavior")]
        [Tooltip("Defines the initial state of the AI's state machine.")]
        [field: SerializeField]
        public EnemyStartingState StartingState { get; private set; } = EnemyStartingState.Chase;

        private IEnemyState currentState;
        public NavMeshAgent Agent { get; private set; }
        public AbilityController Abilities { get; private set; }

        // Cache our states to prevent O(1) garbage allocation on state swaps[cite: 2]
        public ChaseState StateChase { get; } = new();
        public AttackState StateAttack { get; } = new();
        public FleeState StateFlee { get; } = new(); // Newly cached state

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Abilities = GetComponent<AbilityController>();

            // --- Snappy Movement Configuration ---
            // Lowered from 9999f to prevent Transform NaN corruption (the "disappearing" bug)
            Agent.acceleration = 120f;
            Agent.angularSpeed = 1000f;
            Agent.autoBraking = true;
            Agent.updateRotation = true;
        }

        private void Start()
        {
            // 1. Safely bind the agent to the floor before doing anything else
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                // Warp forces the agent onto the legal NavMesh surface instantly
                Agent.Warp(hit.position);
                Agent.isStopped = true;
            }
            else
            {
                Debug.LogError($"[EnemyAI] {name} is not above a baked NavMesh and will break!");
            }

            // 2. Auto-acquire the player via Tag if not manually assigned
            if (Target == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    Target = playerObj.transform;
                else
                    Debug.LogWarning($"[EnemyAI] {name} could not find an object tagged 'Player' in the scene.");
            }

            TurnManager.Instance.RegisterEntity(this);

            // 3. Initialize State
            InitializeStartingState();
        }

        private void OnDestroy()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.UnregisterEntity(this);
        }

        public int Initiative { get; } = 10;

        // --- ITurnEntity Implementation ---[cite: 2]
        public void PlanAction()
        {
            currentState?.PlanAction(this);
        }

        public void DrawIntents()
        {
            // Abilities are drawn by the AbilityController. 
            // If you ever want the enemy to draw a movement path line natively, do it here!
        }

        public void ExecuteAction()
        {
            currentState?.ExecuteAction(this);
        }

        public void EndTurn()
        {
            Agent.isStopped = true;
            Abilities.EndTurn();
        }

        private void InitializeStartingState()
        {
            switch (StartingState)
            {
                case EnemyStartingState.Chase:
                    ChangeState(StateChase);
                    break;
                case EnemyStartingState.Flee:
                    ChangeState(StateFlee);
                    break;
                case EnemyStartingState.Attack:
                    ChangeState(StateAttack);
                    break;
                default:
                    ChangeState(StateChase);
                    break;
            }
        }

        public void ChangeState(IEnemyState newState)
        {
            currentState?.Exit(this);
            currentState = newState;
            currentState?.Enter(this);
        }

        public bool HasLineOfSight()
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 targetPos = Target.position + Vector3.up;
            Vector3 direction = targetPos - origin;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, AttackRange, SightObstacles))
                if (hit.transform != Target)
                    return false;
            return true;
        }
    }

    public class ChaseState : IEnemyState
    {
        public void Enter(EnemyAIController enemy)
        {
        }

        public void PlanAction(EnemyAIController enemy)
        {
            if (enemy.Target == null) return;

            float distanceSqr = (enemy.Target.position - enemy.transform.position).sqrMagnitude;
            bool inRange = distanceSqr <= enemy.AttackRange * enemy.AttackRange;

            if (inRange && enemy.HasLineOfSight())
            {
                enemy.ChangeState(enemy.StateAttack);
                enemy.StateAttack.PlanAction(enemy);
                return;
            }

            enemy.Agent.SetDestination(enemy.Target.position);

            // REMOVED: TurnManager.Instance.RequestTurns(1);
        }

        public void ExecuteAction(EnemyAIController enemy)
        {
            // Unpause the agent so it actually moves during the real-time execution window
            enemy.Agent.isStopped = false;
        }

        public void Exit(EnemyAIController enemy)
        {
            enemy.Agent.isStopped = true;
        }
    }

    public class FleeState : IEnemyState
    {
        private readonly float fleeDistance;
        private readonly float safeDistance;

        public FleeState(float safeDistance = 8f, float fleeDistance = 5f)
        {
            this.safeDistance = safeDistance;
            this.fleeDistance = fleeDistance;
        }

        public void Enter(EnemyAIController enemy)
        {
            enemy.Agent.isStopped = true;
        }

        public void PlanAction(EnemyAIController enemy)
        {
            if (enemy.Target == null) return;

            float distanceSqr = (enemy.Target.position - enemy.transform.position).sqrMagnitude;

            // If the player is far enough away, switch to Attack (Aiming/Shooting)
            if (distanceSqr > safeDistance * safeDistance)
            {
                enemy.ChangeState(enemy.StateAttack);
                enemy.StateAttack.PlanAction(enemy);
                return;
            }

            // Calculate a flee vector pointing away from the target
            Vector3 fleeDirection = (enemy.transform.position - enemy.Target.position).normalized;
            Vector3 targetFleePosition = enemy.transform.position + fleeDirection * fleeDistance;

            // O(1) Spatial Query to ensure the flee point is on the NavMesh
            if (NavMesh.SamplePosition(targetFleePosition, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
                enemy.Agent.SetDestination(hit.position);
        }

        public void ExecuteAction(EnemyAIController enemy)
        {
            enemy.Agent.isStopped = false;
        }

        public void Exit(EnemyAIController enemy)
        {
            enemy.Agent.isStopped = true;
        }
    }

    public class AttackState : IEnemyState
    {
        public void Enter(EnemyAIController enemy)
        {
            // Stop moving to shoot
            enemy.Agent.isStopped = true;
        }

        public void PlanAction(EnemyAIController enemy)
        {
            if (enemy.Target == null) return;

            float distanceSqr = (enemy.Target.position - enemy.transform.position).sqrMagnitude;
            bool inRange = distanceSqr <= enemy.AttackRange * enemy.AttackRange;

            // If the player moved out of range or behind cover, go back to chasing
            if (!inRange || !enemy.HasLineOfSight())
            {
                enemy.ChangeState(enemy.StateChase);
                enemy.StateChase.PlanAction(enemy);
                return;
            }

            // Queue the attack using your existing modular context!
            AbilityContext context = new(
                enemy.gameObject,
                enemy.FirePoint,
                enemy.Target.gameObject,
                enemy.Target.position
            );

            enemy.Abilities.QueueAbility(enemy.ShootAbility, context);
        }

        public void ExecuteAction(EnemyAIController enemy)
        {
            // Face the target visually before shooting
            Vector3 direction = (enemy.Target.position - enemy.transform.position).normalized;
            direction.y = 0; // Keep rotation strictly horizontal
            if (direction != Vector3.zero) enemy.transform.rotation = Quaternion.LookRotation(direction);

            enemy.Abilities.ExecuteAction();
        }

        public void Exit(EnemyAIController enemy)
        {
        }
    }
}