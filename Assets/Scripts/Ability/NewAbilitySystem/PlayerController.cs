using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Ability.NewAbilitySystem
{
    public interface IPlayerState
    {
        void Enter(PlayerController player);

        void HandleAiming(PlayerController player, Vector3 aimPosition);

        void HandleMoveInput(PlayerController player, Vector3 destination);
        void HandleAbilityInput(PlayerController player, AbilityData ability, Vector3 targetPosition);
        void ExecuteTurn(PlayerController player);
        void EndTurn(PlayerController player);
        void Exit(PlayerController player);
    }

    [RequireComponent(typeof(NavMeshAgent), typeof(AbilityController))]
    public class PlayerController : MonoBehaviour, ITurnEntity
    {
        [Header("Abilities")] public AbilityData BasicMoveAbility; // Contains your MoveEffect

        public AbilityData PrimaryAbility;
        public AbilityData SecondaryAbility;
        public AbilityData DashAbility;
        public AbilityData UltimateAbility;

        [Header("Input Settings")] [Tooltip("The layer used to detect mouse clicks for movement/aiming.")]
        public LayerMask GroundLayer;

        [Header("Combat References")] [Tooltip("The transform where projectiles should spawn (e.g., the gun barrel)")]
        public Transform FirePoint;

        // NEW: Cache the mouse position so we can draw intents at the cursor location
        private Vector3 currentAimPosition;

        private IPlayerState currentState;

        private Camera mainCamera;
        private bool usePrimaryAbility = true;

        public NavMeshAgent Agent { get; private set; }
        public AbilityController Abilities { get; private set; }

        // Cached States (Zero Allocation)
        public PlayerExplorationState StateExploration { get; } = new();
        public PlayerCombatState StateCombat { get; } = new();

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Abilities = GetComponent<AbilityController>();
            mainCamera = Camera.main;

            // --- Snappy Movement Configuration ---
            Agent.acceleration = 9999f;
            Agent.angularSpeed = 9999f;
            Agent.autoBraking = true;
        }

        private void Start()
        {
            TurnManager.Instance.RegisterEntity(this);
            GameModeManager.OnGameModeChanged += HandleGameModeChanged;

            // Sync initial state
            HandleGameModeChanged(GameModeManager.Instance.CurrentMode);
        }

        private void Update()
        {
            HandleInput();
        }

        private void OnDestroy()
        {
            if (TurnManager.Instance != null) TurnManager.Instance.UnregisterEntity(this);
            GameModeManager.OnGameModeChanged -= HandleGameModeChanged;
        }

        // --- ITurnEntity Implementation ---
        public int Initiative => 100;

        public void PlanAction()
        {
            /* Handled entirely by user input in Update */
        }

        public void DrawIntents()
        {
            // We only want to draw aiming intents during the combat pause
            if (currentState != StateCombat) return;

            AbilityData activeAbility = usePrimaryAbility ? PrimaryAbility : SecondaryAbility;

            if (activeAbility != null)
            {
                // Construct a dynamic context based on where the mouse is right now
                AbilityContext aimContext = new(gameObject, FirePoint, null, currentAimPosition);

                // Ask the ability to draw itself exactly as the enemies do
                foreach (IAbilityEffect effect in activeAbility.effects)
                    if (effect is IPreviewableEffect preview)
                        preview.DrawPreview(aimContext, IntentDrawer.Instance);
            }
        }

        public void ExecuteAction()
        {
            currentState?.ExecuteTurn(this);
        }

        public void EndTurn()
        {
            currentState?.EndTurn(this);
        }

        private void HandleInput()
        {
            // Safeguard against missing devices
            if (Keyboard.current == null || Mouse.current == null) return;

            if (!TryGetMousePosition(out Vector3 targetPos)) return;

            // NEW: Cache the target position for the DrawIntents cycle
            currentAimPosition = targetPos;

            // Continuously update aiming based on the cursor position
            currentState?.HandleAiming(this, targetPos);

            // 1. Swap Primary/Secondary (Tab)
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                usePrimaryAbility = !usePrimaryAbility;
                Debug.Log($"[Player] Switched to {(usePrimaryAbility ? "Primary" : "Secondary")} Ability.");
            }

            // 3. Movement (Left Click)
            if (Mouse.current.leftButton.wasPressedThisFrame) currentState?.HandleMoveInput(this, targetPos);

            // 4. Primary/Secondary Cast (Right Click)
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                AbilityData activeAbility = usePrimaryAbility ? PrimaryAbility : SecondaryAbility;
                currentState?.HandleAbilityInput(this, activeAbility, targetPos);
            }

            // 5. Dash/Mobility (Shift)
            if (Keyboard.current.leftShiftKey.wasPressedThisFrame)
                currentState?.HandleAbilityInput(this, DashAbility, targetPos);

            // 6. Ultimate (R)
            if (Keyboard.current.rKey.wasPressedThisFrame)
                currentState?.HandleAbilityInput(this, UltimateAbility, targetPos);
        }

        private bool TryGetMousePosition(out Vector3 position)
        {
            position = Vector3.zero;
            if (Mouse.current == null) return false;

            // Read the screen position from the new Input System
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, GroundLayer))
            {
                position = hit.point;
                return true;
            }

            return false;
        }

        private void HandleGameModeChanged(GameMode newMode)
        {
            if (newMode == GameMode.Exploration) ChangeState(StateExploration);
            else ChangeState(StateCombat);
        }

        private void ChangeState(IPlayerState newState)
        {
            currentState?.Exit(this);
            currentState = newState;
            currentState?.Enter(this);
        }
    }

    // --- STATES REMAIN EXACTLY THE SAME ---

    public class PlayerExplorationState : IPlayerState
    {
        public void Enter(PlayerController player)
        {
            player.Agent.isStopped = false; // Allow free movement
        }

        public void HandleAiming(PlayerController player, Vector3 aimPosition)
        {
        }

        public void HandleMoveInput(PlayerController player, Vector3 destination)
        {
            player.Agent.SetDestination(destination);
        }

        public void HandleAbilityInput(PlayerController player, AbilityData ability, Vector3 targetPosition)
        {
            if (ability == null) return;
            AbilityContext context = new(player.gameObject, targetPosition: targetPosition);
            ability.TryCast(context);
        }

        public void ExecuteTurn(PlayerController player)
        {
        }

        public void EndTurn(PlayerController player)
        {
        }

        public void Exit(PlayerController player)
        {
            player.Agent.isStopped = true;
            player.Agent.ResetPath();
        }
    }

    public class PlayerCombatState : IPlayerState
    {
        public void Enter(PlayerController player)
        {
            player.Agent.isStopped = true;
        }

        public void HandleAiming(PlayerController player, Vector3 aimPosition)
        {
            if (TurnManager.Instance.IsExecuting) return;

            Vector3 lookDirection = aimPosition - player.transform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.001f) player.transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        public void HandleMoveInput(PlayerController player, Vector3 destination)
        {
            if (player.BasicMoveAbility == null) return;
            AbilityContext context = new(player.gameObject, targetPosition: destination);

            if (player.Abilities.QueueAbility(player.BasicMoveAbility, context)) TurnManager.Instance.RequestTurns(1);
        }

        public void HandleAbilityInput(PlayerController player, AbilityData ability, Vector3 targetPosition)
        {
            if (ability == null) return;
            AbilityContext context = new(player.gameObject, player.FirePoint, null, targetPosition);

            if (player.Abilities.QueueAbility(ability, context)) TurnManager.Instance.RequestTurns(1);
        }

        public void ExecuteTurn(PlayerController player)
        {
            player.Agent.isStopped = false;
            player.Abilities.ExecuteAction();
        }

        public void EndTurn(PlayerController player)
        {
            player.Agent.isStopped = true;
            if (player.Agent.hasPath) player.Agent.ResetPath();
            player.Abilities.EndTurn();
        }

        public void Exit(PlayerController player)
        {
        }
    }
}