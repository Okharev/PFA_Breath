using System;
using System.Collections.Generic;
using Ability.NewAbilitySystem.UI;
using Dialogues.UI;
using Skills;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UIElements; // NEW: Required for UI Click-Through Prevention

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
        [Header("Default Innate Abilities")] 
        [Tooltip("The foundational movement ability triggered on Left-Click.")]
        public AbilityData BasicMoveAbility;

        // --- DEFAULT LOADOUT FIELDS ---
        public AbilityData DefaultPrimary;
        public AbilityData DefaultSecondary;
        public AbilityData DefaultDash;
        public AbilityData DefaultSpecial;
        public AbilityData DefaultReload;

        [Header("Input Settings")]
        public LayerMask GroundLayer;
        public LayerMask InteractableLayer;

        [Header("Combat References")] 
        public Transform FirePoint;

        private readonly Dictionary<AbilitySlot, AbilityData> activeLoadout = new();
        private AbilitySlot currentActiveSlot = AbilitySlot.Primary;

        private Vector3 currentAimPosition;
        private IPlayerState currentState;
        private Camera mainCamera;

        public event Action<AbilitySlot> OnActiveSlotChanged;
        
        public NavMeshAgent Agent { get; private set; }
        public AbilityController Abilities { get; private set; }

        public PlayerExplorationState StateExploration { get; } = new();
        public PlayerCombatState StateCombat { get; } = new();

        [SerializeField] private UIDocument _mainUI;

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

            // --- INITIALIZE DEFAULTS ---
            EquipLocalSlot(AbilitySlot.Primary, DefaultPrimary);
            EquipLocalSlot(AbilitySlot.Secondary, DefaultSecondary);
            EquipLocalSlot(AbilitySlot.Dash, DefaultDash);
            EquipLocalSlot(AbilitySlot.Special, DefaultSpecial);
            EquipLocalSlot(AbilitySlot.Reload, DefaultReload);
            
            OnActiveSlotChanged?.Invoke(currentActiveSlot);
        }

        private void Update()
        {
            HandleInput();
        }
        
        private bool IsPointerOverUI()
        {
            // Check 1: Traditional Event System Check
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return true;

            // Check 2: Scaled UI Toolkit Raycast Fallback
            if (_mainUI != null && _mainUI.rootVisualElement != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                
                // 1. Invert Y-Axis (Input System is bottom-left, UI is top-left)
                Vector2 screenPos = new Vector2(mousePos.x, Screen.height - mousePos.y);
                
                // 2.onvert Screen Space to UI Panel Space!
                // This guarantees the raycast hits perfectly even if the UI is scaled or letterboxed.
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_mainUI.rootVisualElement.panel, screenPos);
                
                // 3. Fire a raycast directly into the UI Panel using the corrected coordinates
                VisualElement picked = _mainUI.rootVisualElement.panel.Pick(panelPos);
                
                // Walk up the UI tree
                VisualElement current = picked;
                while (current != null)
                {
                    // Use CSS Class Names. This is completely immune to C# inheritance issues
                    // and perfectly matches the background images you click on.
                    if (current.ClassListContains("spell-slot") || 
                        current.ClassListContains("ammo-display") || 
                        current.ClassListContains("skip-turn-button"))
                    {
                        return true; // Block 3D movement
                    }
                    
                    // Move up to check the parent wrapper
                    current = current.parent;
                }
            }
            
            // If the loop finishes without finding a button, we clicked empty space.
            return false; 
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

        public void PlanAction() {}

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

        public void ExecuteAction() => currentState?.ExecuteTurn(this);
        public void EndTurn() => currentState?.EndTurn(this);

        // --- LOCAL EVENT BUS ---
        public event Action<AbilitySlot, AbilityData> OnLoadoutChanged;

        // --- LOADOUT ROUTING LOGIC ---
        private void HandleSkillTreeEquip(AbilityData ability, AbilitySlot slot, int level)
        {
            EquipLocalSlot(slot, ability);
        }

        private void HandleSkillTreeUnequip(AbilitySlot slot)
        {
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
                AbilitySlot.Reload => DefaultReload,
                _ => null
            };
        }

        private void HandleInput()
        {
            if (Skills.UI.SkillTreeUIController.IsOpen || DialogueUIController.IsDialogueOpen) 
                return;

            if (Keyboard.current == null || Mouse.current == null) return;

            // 4. USE OUR NEW BULLETPROOF CHECK
            if (IsPointerOverUI())
                return;

            // 2. CRITICAL UI FIX: Prevent Click-Through
            // Ignore 3D world raycasts/actions if the pointer is resting on a UI element
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (TryGetMousePosition(out Vector3 targetPos))
            {
                currentAimPosition = targetPos;
                currentState?.HandleAiming(this, targetPos);
            }

            // 4. Handle Left Click (Interact vs Move)
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        
                // Prioritize checking for Interactables
                if (Physics.Raycast(ray, out RaycastHit hit, 100f, InteractableLayer))
                {
                    if (hit.collider.TryGetComponent(out IInteractable interactable))
                    {
                        interactable.Interact(gameObject);
                        return; // Consume the input so we don't accidentally walk
                    }
                }

                // Fallback to Movement (Uses the existing targetPos to save processing power)
                currentState?.HandleMoveInput(this, targetPos);
            }

            // 5. Handle Ability Inputs
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                currentActiveSlot = currentActiveSlot == AbilitySlot.Primary ? AbilitySlot.Secondary : AbilitySlot.Primary;
                OnActiveSlotChanged?.Invoke(currentActiveSlot);
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
                if (activeLoadout.TryGetValue(currentActiveSlot, out AbilityData activeAbility))
                    currentState?.HandleAbilityInput(this, activeAbility, targetPos);

            if (Keyboard.current.leftShiftKey.wasPressedThisFrame)
                if (activeLoadout.TryGetValue(AbilitySlot.Dash, out AbilityData dashAbility))
                    currentState?.HandleAbilityInput(this, dashAbility, targetPos);

            if (Keyboard.current.fKey.wasPressedThisFrame)
                if (activeLoadout.TryGetValue(AbilitySlot.Special, out AbilityData specialAbility))
                    currentState?.HandleAbilityInput(this, specialAbility, targetPos);
            
            if (Keyboard.current.rKey.wasPressedThisFrame)
                if (activeLoadout.TryGetValue(AbilitySlot.Reload, out AbilityData reloadAbility)) // Fixed variable name
                    currentState?.HandleAbilityInput(this, reloadAbility, targetPos);
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

    // --- STATES ---
    public class PlayerExplorationState : IPlayerState
    {
        public void Enter(PlayerController player) => player.Agent.isStopped = false;
        public void HandleAiming(PlayerController player, Vector3 aimPosition) {}

        public void HandleMoveInput(PlayerController player, Vector3 destination)
        {
            player.Agent.SetDestination(destination);
        }

        public void ExecuteTurn(PlayerController player) {}
        public void EndTurn(PlayerController player) {}

        public void HandleAbilityInput(PlayerController player, AbilityData ability, Vector3 targetPosition)
        {
            if (ability == player.DefaultDash)
            {
                AbilityContext context = new AbilityContext(player.DefaultDash, player.gameObject, player.FirePoint, null, targetPosition);
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
            SetAgentStoppedSafely(player.Agent, true);
        }

        public void ExecuteTurn(PlayerController player)
        {
            SetAgentStoppedSafely(player.Agent, false);
            player.Abilities.ExecuteAction();
        }

        public void Exit(PlayerController player) {}

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
            if (TurnManager.Instance.IsExecuting) return;

            if (player.BasicMoveAbility == null) return;
            AbilityContext context = new(player.BasicMoveAbility, player.gameObject, targetPosition: destination);

            if (player.Abilities.QueueAbility(player.BasicMoveAbility, context))
                TurnManager.Instance.RequestTurns(1);
        }
        
        public void HandleAbilityInput(PlayerController player, AbilityData ability, Vector3 targetPosition)
        {
            if (TurnManager.Instance.IsExecuting) return;
            if (ability == null) return;

            AbilityContext context = new(ability, player.gameObject, player.FirePoint, null, targetPosition);

            if (ability.turnCost == 0)
            {
                player.Abilities.TryExecuteImmediate(ability, context);
            }
            else
            {
                if (player.Abilities.QueueAbility(ability, context))
                {
                    TurnManager.Instance.RequestTurns(ability.turnCost);
                }
            }
        }

        public void EndTurn(PlayerController player)
        {
            SetAgentStoppedSafely(player.Agent, true);
            
            if (player.Agent != null && player.Agent.isActiveAndEnabled && player.Agent.isOnNavMesh)
            {
                if (player.Agent.hasPath) player.Agent.ResetPath();
            }
            
            player.Abilities.EndTurn();
        }

        private void SetAgentStoppedSafely(NavMeshAgent agent, bool stop)
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = stop;
            }
        }
    }
}