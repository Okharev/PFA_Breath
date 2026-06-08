using System;
using UnityEngine;
using UnityEngine.AI;

namespace Ability.NewAbilitySystem
{
    /// <summary>
    ///     Bridges the generic HSM with the turn-based AI Controller.
    /// </summary>
    public abstract class EnemyBaseState : IState
    {
        protected readonly EnemyAIController Enemy;

        protected EnemyBaseState(EnemyAIController enemy)
        {
            Enemy = enemy;
        }

        public virtual void OnEnter()
        {
        }

        /// <summary>
        ///     Maps to the turn-based PlanAction phase.
        ///     Evaluates logic during the paused planning phase.
        /// </summary>
        public virtual void OnUpdate()
        {
        }

        public virtual void OnExit()
        {
        }

        /// <summary>
        ///     Executes the physical action when the real-time turn execution begins.
        /// </summary>
        public abstract void ExecuteAction();
    }

    public class HSMChaseState : EnemyBaseState
    {
        public HSMChaseState(EnemyAIController enemy) : base(enemy)
        {
        }

        public override void OnUpdate()
        {
            if (Enemy.Target != null)
                Enemy.Agent.SetDestination(Enemy.Target.position);
        }

        public override void ExecuteAction()
        {
            Enemy.Agent.isStopped = false; // Unpause for movement
        }

        public override void OnExit()
        {
            Enemy.Agent.isStopped = true;
        }
    }

    public class HSMFleeState : EnemyBaseState
    {
        private readonly float fleeDistance;

        public HSMFleeState(EnemyAIController enemy, float fleeDistance = 8f) : base(enemy)
        {
            this.fleeDistance = fleeDistance;
        }

        public override void OnEnter()
        {
            Enemy.Agent.isStopped = true;
        }

        public override void OnUpdate()
        {
            if (Enemy.Target == null) return;

            Vector3 fleeDirection = (Enemy.transform.position - Enemy.Target.position).normalized;
            Vector3 targetFleePosition = Enemy.transform.position + fleeDirection * fleeDistance;

            // O(1) Spatial Query ensuring NavMesh compliance
            if (NavMesh.SamplePosition(targetFleePosition, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
                Enemy.Agent.SetDestination(hit.position);
        }

        public override void ExecuteAction()
        {
            Enemy.Agent.isStopped = false;
        }

        public override void OnExit()
        {
            Enemy.Agent.isStopped = true;
        }
    }

    public class HSMAttackState : EnemyBaseState
    {
        public HSMAttackState(EnemyAIController enemy) : base(enemy)
        {
        }

        public override void OnEnter()
        {
            Enemy.Agent.isStopped = true; // Stop moving to aim
        }

        public override void OnUpdate()
        {
            if (Enemy.Target == null || Enemy.PrimaryAbility == null) return;

            // Prevent queuing multiple times if already channeling a shot
            if (Enemy.Abilities.HasQueuedAbility) return;

            AbilityContext context = new(
                null,
                Enemy.gameObject,
                Enemy.FirePoint,
                Enemy.Target.gameObject,
                Enemy.Target.position
            );

            Enemy.Abilities.QueueAbility(Enemy.PrimaryAbility, context);
        }

        public override void ExecuteAction()
        {
            // Face target horizontally
            Vector3 direction = (Enemy.Target.position - Enemy.transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
                Enemy.transform.rotation = Quaternion.LookRotation(direction);

            Enemy.Abilities.ExecuteAction();
        }
    }

    public enum EnemyArchetype
    {
        Rifleman, // Mid-range LoS Shooter
        Hugger, // Melee AoE
        Sniper // Long-range, kites if approached
    }

    [RequireComponent(typeof(NavMeshAgent), typeof(AbilityController))]
    public class EnemyAIController : MonoBehaviour, ITurnEntity
    {
        [field: Header("Targeting & Physics")]
        [field: SerializeField]
        public Transform Target { get; private set; }

        [field: SerializeField] public AbilityData PrimaryAbility { get; private set; }
        [field: SerializeField] public Transform FirePoint { get; private set; }
        [field: SerializeField] public LayerMask SightObstacles { get; private set; }

        [field: Header("Archetype Specs")]
        [field: SerializeField]
        public EnemyArchetype Archetype { get; private set; } = EnemyArchetype.Rifleman;

        [field: SerializeField] public float AttackRange { get; private set; } = 15f;
        [field: SerializeField] public float SafeDistance { get; private set; } = 8f; // Used by Snipers

        public NavMeshAgent Agent { get; private set; }
        public AbilityController Abilities { get; private set; }
        public StateMachine Brain { get; private set; }

        // Cached per turn for O(1) condition checks
        public float TargetDistanceSqr { get; private set; }

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Abilities = GetComponent<AbilityController>();

            Agent.acceleration = 120f;
            Agent.angularSpeed = 1000f;
            Agent.autoBraking = true;
            Agent.updateRotation = true;
        }

        private void Start()
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                Agent.Warp(hit.position);
                Agent.isStopped = true;
            }
            else
            {
                Debug.LogError($"[EnemyAI] {name} is off the NavMesh!");
            }

            if (Target == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) Target = playerObj.transform;
            }

            TurnManager.Instance.RegisterEntity(this);
            BuildBrain();
        }

        private void OnDestroy()
        {
            if (TurnManager.Instance != null) TurnManager.Instance.UnregisterEntity(this);
        }

        public int Initiative { get; } = 10;

        public void PlanAction()
        {
            // 1. Cache mathematics once per turn
            if (Target != null)
                TargetDistanceSqr = (Target.position - transform.position).sqrMagnitude;

            // 2. StateMachine evaluates zero-GC transitions, then calls OnUpdate() on current state
            Brain?.Update();
        }

        public void DrawIntents()
        {
        }

        public void ExecuteAction()
        {
            // 3. Cast to our bridge state and execute physics/rendering
            if (Brain?.CurrentState is EnemyBaseState activeState) activeState.ExecuteAction();
        }

        public void EndTurn()
        {
            Agent.isStopped = true;
            Abilities.EndTurn();
        }

        public bool HasLineOfSight()
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 targetPos = Target.position + Vector3.up;
            Vector3 direction = targetPos - origin;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, AttackRange, SightObstacles))
                return hit.transform == Target;
            return true;
        }

        // --- THE FACTORY PATTERN ---
        private void BuildBrain()
        {
            Brain = new StateMachine();

            // Instantiate behavior singletons for this enemy
            HSMChaseState chase = new(this);
            HSMAttackState attack = new(this);
            HSMFleeState flee = new(this, SafeDistance + 2f);

            switch (Archetype)
            {
                case EnemyArchetype.Rifleman:
                    // Chases until in range & LoS, then shoots.
                    Brain.AddTransition(chase, attack, () => InRangeAndLoS() && NotChanneling());
                    Brain.AddTransition(attack, chase, () => OutOfRangeOrNoLoS() && NotChanneling());
                    Brain.SetState(chase);
                    break;

                case EnemyArchetype.Hugger:
                    // Similar to Rifleman, but AttackRange acts as melee range. 
                    Brain.AddTransition(chase, attack, () => InRangeAndLoS() && NotChanneling());
                    Brain.AddTransition(attack, chase, () => OutOfRangeOrNoLoS() && NotChanneling());
                    Brain.SetState(chase);
                    break;

                case EnemyArchetype.Sniper:
                    // Complex behavior: Flee if rushed, Snipe if at perfect range, chase if too far.
                    Brain.AddTransition(chase, attack, () => InRangeAndLoS() && IsSafeDistance() && NotChanneling());
                    Brain.AddTransition(chase, flee, () => IsTooClose() && NotChanneling());

                    Brain.AddTransition(attack, flee, () => IsTooClose() && NotChanneling());
                    Brain.AddTransition(attack, chase, () => OutOfRangeOrNoLoS() && NotChanneling());

                    Brain.AddTransition(flee, attack, () => IsSafeDistance() && InRangeAndLoS() && NotChanneling());
                    Brain.AddTransition(flee, chase, () => IsSafeDistance() && !InRangeAndLoS() && NotChanneling());

                    Brain.SetState(chase);
                    break;
            }

            return;

            // CRITICAL: Prevent interrupting channeled abilities (like a Sniper aiming)
            bool NotChanneling() => !Abilities.HasQueuedAbility;

            // Reusable, zero-allocation condition delegates
            bool InRangeAndLoS() => TargetDistanceSqr <= AttackRange * AttackRange && HasLineOfSight();

            bool OutOfRangeOrNoLoS() => TargetDistanceSqr > AttackRange * AttackRange || !HasLineOfSight();

            bool IsTooClose() => TargetDistanceSqr < SafeDistance * SafeDistance;

            bool IsSafeDistance() => TargetDistanceSqr >= SafeDistance * SafeDistance;
        }
    }
}