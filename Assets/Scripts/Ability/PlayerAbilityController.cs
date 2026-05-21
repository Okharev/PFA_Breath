using System;
using System.Collections.Generic; // Added for the loadout list
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ability
{
    [RequireComponent(typeof(PlayerOxygen), typeof(PlayerController))]
    public class PlayerAbilityController : MonoBehaviour
    {
        [Header("Aiming Settings")] 
        [Tooltip("Must match the floor layer so mouse raycasts ignore walls/enemies.")]
        public LayerMask floorMask;

        [Header("References")] 
        public Transform firePoint;
        public ActionVisualizer actionVisualizer;
        public GameObject defaultProjectile; 
        public GameObject sniperProjectile; 

        // --- Expose active weapon for UI styling (e.g., highlighting the active slot) ---
        public IAbility ActiveWeaponAbility { get; private set; }

        // Core Dependencies
        private Camera mainCam;
        private PlayerOxygen oxygen;
        private PlayerController physicsController;

        // Ability Slots
        private IAbility movementAbility;
        private IAbility dashAbility;
        private IAbility primaryAbility;
        private IAbility secondaryAbility;
        private IAbility specialAbility;

        private void Awake() 
        {
            oxygen = GetComponent<PlayerOxygen>();
            physicsController = GetComponent<PlayerController>();
            mainCam = Camera.main;

            // Now, these are guaranteed to exist before the UI asks for them in Start()
            movementAbility = new MovementAbility(physicsController, 5f);
            primaryAbility = new ShotgunAbility(defaultProjectile, firePoint, 6, 12f);
            secondaryAbility = new SniperAbility(sniperProjectile, firePoint);
            specialAbility = new TeleportAbility(physicsController, 15f);
            dashAbility = new DashAbility(physicsController, 8f); // 8 units dash range

            ActiveWeaponAbility = primaryAbility;
        }

        // --- Public API for the UI Controller ---
        /// <summary>
        /// Returns the player's current loadout so the UI Toolkit can dynamically build the Action Bar.
        /// </summary>
        public List<IAbility> GetLoadout()
        {
            return new List<IAbility> 
            { 
                movementAbility, 
                primaryAbility, 
                secondaryAbility, 
                dashAbility,
                specialAbility 
            };
        }

        private void Update()
        {
            if (Mouse.current == null || Keyboard.current == null || mainCam == null) return;

            if (!TurnManager.Instance.IsExecuting)
            {
                HandleModeSwitching();
                HandleAimingAndExecution();
            }
            else
            {
                actionVisualizer?.Hide();
            }
        }

        private void OnDestroy()
        {
            if (secondaryAbility is IDisposable disposableSecondary) disposableSecondary.Dispose();
            if (specialAbility is IDisposable disposableSpecial) disposableSpecial.Dispose();
            if (dashAbility is IDisposable disposableDash) disposableDash.Dispose();
        }

        private void HandleModeSwitching()
        {
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ActiveWeaponAbility = (ActiveWeaponAbility == primaryAbility) ? secondaryAbility : primaryAbility;
                Debug.Log($"Switched weapon to: {ActiveWeaponAbility.AbilityId}");
                
                // Optional: You could fire an event here to tell the UI to instantly highlight the new active weapon
            }
        
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                AttemptAbility(specialAbility, GetMouseWorldPosition());
            }
            
            if (Keyboard.current.leftShiftKey.wasPressedThisFrame)
            {
                AttemptAbility(dashAbility, GetMouseWorldPosition());
            }
        }

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

            movementAbility.DrawPreview(context);
            ActiveWeaponAbility.DrawPreview(context);

            if (mouseTarget.HasValue)
            {
                physicsController.LookAtTarget(mouseTarget.Value);
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                AttemptAbility(movementAbility, mouseTarget);
            }
            else if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                AttemptAbility(ActiveWeaponAbility, mouseTarget);
            }
        }

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
        
        private Vector3? GetMouseWorldPosition()
        {
            Ray ray = mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, floorMask)) return hit.point;
            return null;
        }
    }
}