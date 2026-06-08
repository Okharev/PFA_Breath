using System;
using System.Collections.Generic;
using Dialogues.UI;
using Skills;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

// NEW: Required for AbilitySlot enum and SkillTreeManager events

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
        [Header("Default Innate Abilities")] [Tooltip("The foundational movement ability triggered on Left-Click.")]
        public AbilityData BasicMoveAbility;

        // --- DEFAULT LOADOUT FIELDS ---
        public AbilityData DefaultPrimary;
        public AbilityData DefaultSecondary;
        public AbilityData DefaultDash;
        public AbilityData DefaultSpecial;

        [Header("Input Settings")]
        public LayerMask GroundLayer;
        public LayerMask InteractableLayer;

        [Header("Combat References")] public Transform FirePoint;

        private readonly Dictionary<AbilitySlot, AbilityData> activeLoadout = new();
        private AbilitySlot currentActiveSlot = AbilitySlot.Primary;

        private Vector3 currentAimPosition;
        private IPlayerState currentState;
        private Camera mainCamera;

        public NavMeshAgent Agent { get; private set; }
        public AbilityController Abilities { get; private set; }

        public PlayerExplorationState StateExploration { get; } = new();
        public PlayerCombatState StateCombat { get; } = new();

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Abilities = GetComponent<AbilityController>();
            mainCamera = Camera.main;

            Agent.acceleration = 9999f;
            Agent.angularSpeed = 9999f;
            Agent.autoBraking = true;
        }

        private void Start()
        {
            TurnManager.Instance.RegisterEntity(this);
            GameModeManager.OnGameModeChanged += HandleGameModeChanged;
            HandleGameModeChanged(GameModeManager.Instance.CurrentMode);

            // --- NEW: INITIALIZE DEFAULTS ---
            EquipLocalSlot(AbilitySlot.Primary, DefaultPrimary);
            EquipLocalSlot(AbilitySlot.Secondary, DefaultSecondary);
            EquipLocalSlot(AbilitySlot.Dash, DefaultDash);
            EquipLocalSlot(AbilitySlot.Special, DefaultSpecial);
        }

        private void Update()
        {
            HandleInput();
        }

        private void OnEnable()
        {
            SkillTreeManager.OnAbilityEquipped += HandleSkillTreeEquip;
            SkillTreeManager.OnAbilityUnequipped += HandleSkillTreeUnequip;
        }

        private void OnDisable()
        {
            SkillTreeManager.OnAbilityEquipped -= HandleSkillTreeEquip;
            SkillTreeManager.OnAbilityUnequipped -= HandleSkillTreeUnequip;
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
        }

        public void DrawIntents()
        {
            if (currentState != StateCombat) return;

            if (activeLoadout.TryGetValue(currentActiveSlot, out AbilityData activeAbility))
            {
                AbilityContext aimContext = new(activeAbility, gameObject, FirePoint, null, currentAimPosition);
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

        // --- NEW: LOCAL EVENT BUS ---
        // UI will listen to this so it knows exactly what the player is holding!
        public event Action<AbilitySlot, AbilityData> OnLoadoutChanged;

        // --- LOADOUT ROUTING LOGIC ---
        private void HandleSkillTreeEquip(AbilityData ability, AbilitySlot slot, int level)
        {
            EquipLocalSlot(slot, ability);
        }

        private void HandleSkillTreeUnequip(AbilitySlot slot)
        {
            // When unequipped from the tree, revert to the baseline default
            EquipLocalSlot(slot, GetDefaultAbilityForSlot(slot));
        }

        private void EquipLocalSlot(AbilitySlot slot, AbilityData ability)
        {
            if (ability == null)
            {
                activeLoadout.Remove(slot);
                OnLoadoutChanged?.Invoke(slot, null);
            }
            else
            {
                activeLoadout[slot] = ability;
                OnLoadoutChanged?.Invoke(slot, ability);
            }
        }

        private AbilityData GetDefaultAbilityForSlot(AbilitySlot slot)
        {
            return slot switch
            {
                AbilitySlot.Primary => DefaultPrimary,
                AbilitySlot.Secondary => DefaultSecondary,
                AbilitySlot.Dash => DefaultDash,
                AbilitySlot.Special => DefaultSpecial,
                _ => null
            };
        }

        private void HandleInput()
        {
            if (Skills.UI.SkillTreeUIController.IsOpen || DialogueUIController.IsDialogueOpen) 
                return;

            if (Keyboard.current == null || Mouse.current == null) return;

            // We still want to track aiming for abilities
            if (TryGetMousePosition(out Vector3 targetPos))
            {
                currentAimPosition = targetPos;
                currentState?.HandleAiming(this, targetPos);
            }

            // --- Handle Left Click (Interact vs Move) ---
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        
                // 1. Prioritize checking for Interactables
                if (Physics.Raycast(ray, out RaycastHit hit, 100f, InteractableLayer))
                {
                    if (hit.collider.TryGetComponent(out IInteractable interactable))
                    {
                        // Trigger the interaction and consume the input
                        interactable.Interact(gameObject);
                        return; 
                    }
                }

                // 2. Fallback to standard Movement
                if (TryGetMousePosition(out Vector3 moveTarget))
                {
                    currentState?.HandleMoveInput(this, moveTarget);
                }
            }
            
            currentAimPosition = targetPos;
            currentState?.HandleAiming(this, targetPos);

            if (Keyboard.current.tabKey.wasPressedThisFrame)
                currentActiveSlot = currentActiveSlot == AbilitySlot.Primary
                    ? AbilitySlot.Secondary
                    : AbilitySlot.Primary;

            if (Mouse.current.leftButton.wasPressedThisFrame)
                currentState?.HandleMoveInput(this, targetPos);

            if (Mouse.current.rightButton.wasPressedThisFrame)
                if (activeLoadout.TryGetValue(currentActiveSlot, out AbilityData activeAbility))
                    currentState?.HandleAbilityInput(this, activeAbility, targetPos);

            if (Keyboard.current.leftShiftKey.wasPressedThisFrame)
                if (activeLoadout.TryGetValue(AbilitySlot.Dash, out AbilityData dashAbility))
                    currentState?.HandleAbilityInput(this, dashAbility, targetPos);

            if (Keyboard.current.rKey.wasPressedThisFrame)
                if (activeLoadout.TryGetValue(AbilitySlot.Special, out AbilityData specialAbility))
                    currentState?.HandleAbilityInput(this, specialAbility, targetPos);
        }

        private bool TryGetMousePosition(out Vector3 position)
        {
            position = Vector3.zero;
            if (Mouse.current == null) return false;

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

    // --- STATES (UNTOUCHED) ---

    public class PlayerExplorationState : IPlayerState
    {
        public void Enter(PlayerController player)
        {
            player.Agent.isStopped = false;
        }

        public void HandleAiming(PlayerController player, Vector3 aimPosition)
        {
        }

        public void HandleMoveInput(PlayerController player, Vector3 destination)
        {
            player.Agent.SetDestination(destination);
        }

        public void ExecuteTurn(PlayerController player)
        {
        }

        public void EndTurn(PlayerController player)
        {
        }

        public void HandleAbilityInput(PlayerController player, AbilityData ability, Vector3 targetPosition)
        {
            // Only allow the Dash ability to be fired during exploration
            if (ability == player.DefaultDash)
            {
                AbilityContext context = new AbilityContext(player.DefaultDash, player.gameObject, player.FirePoint, null, targetPosition);
                
                // Use the TryExecuteImmediate method you already built for real-time firing!
                player.Abilities.TryExecuteImmediate(ability, context);
            }
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

        public void ExecuteTurn(PlayerController player)
        {
            player.Agent.isStopped = false;
            player.Abilities.ExecuteAction();
        }

        public void Exit(PlayerController player)
        {
        }

        public void HandleAiming(PlayerController player, Vector3 aimPosition)
        {
            if (TurnManager.Instance.IsExecuting) return;

            Vector3 lookDirection = aimPosition - player.transform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.001f)
                player.transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        public void HandleMoveInput(PlayerController player, Vector3 destination)
        {
            if (player.BasicMoveAbility == null) return;
            AbilityContext context = new(player.BasicMoveAbility, player.gameObject, targetPosition: destination);

            if (player.Abilities.QueueAbility(player.BasicMoveAbility, context))
                TurnManager.Instance.RequestTurns(1);
        }

        public void HandleAbilityInput(PlayerController player, AbilityData ability, Vector3 targetPosition)
        {
            if (ability == null) return;
            AbilityContext context = new(ability, player.gameObject, player.FirePoint, null, targetPosition);

            // --- ROUTING BASED ON TURN COST ---
            if (ability.turnCost == 0)
            {
                // Path A: Free Action (e.g., Phase 1 Dash)
                // Executes immediately during the paused planning phase. 
                // The TurnManager is NOT told to advance time.
                player.Abilities.TryExecuteImmediate(ability, context);
            }
            else
            {
                // Path B: Standard Turn-Ending Action
                if (player.Abilities.QueueAbility(ability, context))
                {
                    // Dynamically request turns based on the ability's actual cost
                    TurnManager.Instance.RequestTurns(ability.turnCost);
                }
            }
        }

        public void EndTurn(PlayerController player)
        {
            player.Agent.isStopped = true;
            if (player.Agent.hasPath) player.Agent.ResetPath();
            player.Abilities.EndTurn();
        }
    }
}