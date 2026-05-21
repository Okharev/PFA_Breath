using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ability
{
    /// <summary>
    ///     The central input and state manager for the player.
    ///     Determines what ability is active, polls for user input, draws UI previews,
    ///     and deducts global resources (Oxygen/Time) before executing an ability.
    /// </summary>
    [RequireComponent(typeof(PlayerOxygen), typeof(PlayerController))]
    public class PlayerAbilityController : MonoBehaviour
    {
        [Header("Aiming Settings")] [Tooltip("Must match the floor layer so mouse raycasts ignore walls/enemies.")]
        public LayerMask floorMask;

        [Header("References")] public Transform firePoint;

        public ActionVisualizer actionVisualizer;
        public GameObject defaultProjectile; // Placeholder for demo purposes
        public GameObject sniperProjectile; // Placeholder for demo purposes

        // State Tracking
        private IAbility activeWeaponAbility;

        // Core Dependencies
        private Camera mainCam;

        // --- ABILITY SLOTS (The Strategy Pattern) ---
        private IAbility movementAbility;
        private PlayerOxygen oxygen;
        private PlayerController physicsController;
        private IAbility primaryAbility;
        private IAbility secondaryAbility;
        private IAbility specialAbility;

        private void Start()
        {
            oxygen = GetComponent<PlayerOxygen>();
            physicsController = GetComponent<PlayerController>();
            mainCam = Camera.main;

            // Initialize Loadout (In the future, a Skill Tree/Inventory manager injects these)
            movementAbility = new MovementAbility(physicsController, 5f);
            primaryAbility = new ShotgunAbility(defaultProjectile, firePoint, 6, 12f);
            secondaryAbility = new SniperAbility(sniperProjectile, firePoint);
            specialAbility = new TeleportAbility(15f);

            // Default starting state
            activeWeaponAbility = primaryAbility;
            
        }

        private void Update()
        {
            // Safety check for critical dependencies
            if (Mouse.current == null || Keyboard.current == null || mainCam == null) return;

            // --- PHASE 1: PLANNING PHASE (Time is paused, awaiting input) ---
            if (!TurnManager.Instance.IsExecuting)
            {
                HandleModeSwitching();
                HandleAimingAndExecution();
            }
            else
            {
                // --- PHASE 2: EXECUTION PHASE (Time is flowing) ---
                actionVisualizer?.Hide();
            }
        }

        private void OnDestroy()
        {
            // CRITICAL: Ensure any stateful abilities (like Sniper) unsubscribe from TurnManager
            if (secondaryAbility is IDisposable disposableSecondary) disposableSecondary.Dispose();
            if (specialAbility is IDisposable disposableSpecial) disposableSpecial.Dispose();
        }

        /// <summary>
        ///     Handles swapping which ability is currently bound to Left-Click.
        ///     Because abilities are abstracted, switching them is an O(1) operation with zero GC allocation.
        /// </summary>
        private void HandleModeSwitching()
        {
            // Tab swaps between Primary and Secondary
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                activeWeaponAbility = (activeWeaponAbility == primaryAbility) ? secondaryAbility : primaryAbility;
                Debug.Log($"Switched weapon to: {activeWeaponAbility.AbilityId}");
            }
        
            // R instantly executes the Special Ability 
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                AttemptAbility(specialAbility, GetMouseWorldPosition());
            }
        }

        /// <summary>
        ///     Calculates the mouse position, asks the active ability to draw its UI preview,
        ///     and listens for the execution trigger.
        /// </summary>
        private void HandleAimingAndExecution()
        {
            Vector3? mouseTarget = GetMouseWorldPosition();
        
            AbilityContext context = new AbilityContext
            {
                Caster = gameObject,
                CasterPosition = transform.position,
                MouseWorldPosition = mouseTarget,
                Visualizer = actionVisualizer
            };

            // 1. Draw Previews
            // Because they are decoupled, you can preview both where you will walk 
            // AND where your gun is aiming simultaneously!
            movementAbility.DrawPreview(context);
            activeWeaponAbility.DrawPreview(context);

            if (mouseTarget.HasValue)
            {
                physicsController.LookAtTarget(mouseTarget.Value);
            }

            // 2. Listen for Inputs strictly mapped to their dedicated abilities
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // LEFT CLICK IS STRICTLY MOVEMENT
                AttemptAbility(movementAbility, mouseTarget);
            }
            else if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                // RIGHT CLICK IS STRICTLY THE EQUIPPED WEAPON
                AttemptAbility(activeWeaponAbility, mouseTarget);
            }
        }

        /// <summary>
        ///     Centralized gatekeeper for all abilities. Validates targeting, deducts Oxygen,
        ///     executes the ability logic, and charges the TurnManager.
        /// </summary>
        private void AttemptAbility(IAbility ability, Vector3? targetPos)
        {
            if (ability.RequiresTargeting && !targetPos.HasValue) return;

            AbilityContext context = new AbilityContext
            {
                Caster = gameObject,
                CasterPosition = transform.position,
                MouseWorldPosition = targetPos,
                Visualizer = actionVisualizer
            };
    
            if (!ability.CanExecute(context))
            {
                Debug.Log($"{ability.AbilityId} is not ready or target is invalid!");
                return;
            }

            if (oxygen.TryConsume(ability.OxygenCost))
            {
                actionVisualizer?.Hide();
                ability.Execute(context);
                TurnManager.Instance.ExecuteTurns(ability.TurnCost);

            }
            else
            {
                Debug.Log($"Not enough oxygen for {ability.AbilityId}!");
            }
        }
        
        /// <summary>
        ///     Raycasts from the screen to the 3D floor to find the tactical grid position.
        /// </summary>
        private Vector3? GetMouseWorldPosition()
        {
            Ray ray = mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, floorMask)) return hit.point;
            return null;
        }
    }
}