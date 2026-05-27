using System;
using System.Collections.Generic;
using Skills;
using Skills.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Added for the loadout list

namespace Ability
{
    [RequireComponent(typeof(PlayerOxygen), typeof(PlayerController), typeof(PlayerStats))]
    public class PlayerAbilityController : MonoBehaviour
    {
        [Header("Aiming Settings")] [Tooltip("Must match the floor layer so mouse raycasts ignore walls/enemies.")]
        public LayerMask floorMask;

        [Header("References")] public Transform firePoint;

        public ActionVisualizer actionVisualizer;
        public GameObject defaultProjectile;
        public GameObject sniperProjectile;
        private IAbility dashAbility;

        // Core Dependencies
        private Camera mainCam;

        // Ability Slots
        private IAbility movementAbility;
        private PlayerOxygen oxygen;
        private PlayerController physicsController;
        private PlayerStats playerStats;
        private IAbility primaryAbility;
        private IAbility secondaryAbility;
        private IAbility specialAbility;

        // --- Expose active weapon for UI styling (e.g., highlighting the active slot) ---
        public IAbility ActiveWeaponAbility { get; private set; }

        private void Awake()
        {
            oxygen = GetComponent<PlayerOxygen>();
            physicsController = GetComponent<PlayerController>();
            playerStats = GetComponent<PlayerStats>();
            mainCam = Camera.main;

            movementAbility = new MovementAbility(physicsController, 5f); 
    
            // Set the new Basic Blaster as the default primary
            primaryAbility = new BasicBlasterAbility(defaultProjectile, firePoint, playerStats);
            secondaryAbility = new ShotgunAbility(defaultProjectile, firePoint, playerStats); // Moved Shotgun to secondary

            specialAbility = new TeleportAbility(physicsController, 15f);
    
            // Replace the base dash with the new Ram Dash
            dashAbility = new RamDashAbility(physicsController, playerStats, 8f);

            ActiveWeaponAbility = primaryAbility;
        }

        private void Update()
        {
            // ADD THIS LINE: Stop processing player input if the skill tree is open
            if (SkillTreeUIController.IsOpen) return;

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
            foreach (IAbility ability in GetLoadout())
                if (ability is IDisposable disposableAbility)
                    disposableAbility.Dispose();
        }

        // --- Public API for the UI Controller ---
        /// <summary>
        ///     Returns the player's current loadout so the UI Toolkit can dynamically build the Action Bar.
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

        public void EquipAbility(string abilityId, AbilitySlot slot, int level)
        {
            IAbility newAbility = CreateAbilityById(abilityId);
            if (newAbility == null) return;

            newAbility.SetLevel(level);

            IAbility oldAbility = GetAbilityInSlot(slot);
            if (oldAbility is IDisposable disposableAbility) disposableAbility.Dispose();

            AssignToSlot(slot, newAbility, oldAbility);
            Debug.Log($"[AbilitySystem] Successfully equipped {abilityId} (Level {level}) to {slot} slot.");
        }

        // Safe fallback mechanic to restore base abilities when unequipping a skill tree node
        public void EquipDefaultAbility(AbilitySlot slot)
        {
            IAbility oldAbility = GetAbilityInSlot(slot);
            if (oldAbility is IDisposable disposableAbility) disposableAbility.Dispose();

            IAbility defaultAbility = slot switch
            {
                AbilitySlot.Primary => new ShotgunAbility(defaultProjectile, firePoint, playerStats),
                AbilitySlot.Secondary => new SniperAbility(sniperProjectile, firePoint),
                AbilitySlot.Movement => new MovementAbility(physicsController, 5f),
                AbilitySlot.Dash => new DashAbility(physicsController, 8f),
                AbilitySlot.Special => new TeleportAbility(physicsController, 15f),
                _ => null
            };

            AssignToSlot(slot, defaultAbility, oldAbility);
            Debug.Log($"[AbilitySystem] Slot {slot} reverted to default base ability.");
        }

        // Helper method to keep slot assignment clean and ensure the UI/Weapon Switcher stays synced
        private void AssignToSlot(AbilitySlot slot, IAbility newAbility, IAbility oldAbility)
        {
            switch (slot)
            {
                case AbilitySlot.Primary:
                    primaryAbility = newAbility;
                    if (ActiveWeaponAbility == oldAbility) ActiveWeaponAbility = primaryAbility;
                    break;
                case AbilitySlot.Secondary:
                    secondaryAbility = newAbility;
                    if (ActiveWeaponAbility == oldAbility) ActiveWeaponAbility = secondaryAbility;
                    break;
                case AbilitySlot.Movement: movementAbility = newAbility; break;
                case AbilitySlot.Dash: dashAbility = newAbility; break;
                case AbilitySlot.Special: specialAbility = newAbility; break;
            }
        }

        private IAbility GetAbilityInSlot(AbilitySlot slot)
        {
            return slot switch
            {
                AbilitySlot.Primary => primaryAbility,
                AbilitySlot.Secondary => secondaryAbility,
                AbilitySlot.Movement => movementAbility,
                AbilitySlot.Dash => dashAbility,
                AbilitySlot.Special => specialAbility,
                _ => null
            };
        }

        private IAbility CreateAbilityById(string id)
        {
            return id switch
            {
                "Basic_Move" => new MovementAbility(physicsController, 5f),
                "Blink_Strike" => new TeleportAbility(physicsController, 15f),
                "Evasive_Dash" => new DashAbility(physicsController, 8f),
                "Ram_Dash" => new RamDashAbility(physicsController, playerStats, 8f), // NEW
                "Basic_Blaster" => new BasicBlasterAbility(defaultProjectile, firePoint, playerStats), // NEW
                "Shotgun_Blast" => new ShotgunAbility(defaultProjectile, firePoint, playerStats),
                "Railgun_Sniper_Channeled" => new SniperAbility(sniperProjectile, firePoint),
                _ => null
            };
        }

        private void HandleModeSwitching()
        {
            if (GameModeManager.Instance.CurrentMode == GameMode.Exploration) return;

            // Weapon Swap
            if (Keyboard.current.tabKey.wasPressedThisFrame)
                ActiveWeaponAbility = ActiveWeaponAbility == primaryAbility ? secondaryAbility : primaryAbility;

            // Special Ability (Changed from R to Q to allow R to be Reload)
            if (Keyboard.current.qKey.wasPressedThisFrame)
                AttemptAbility(specialAbility, GetMouseWorldPosition());

            if (Keyboard.current.leftShiftKey.wasPressedThisFrame)
                AttemptAbility(dashAbility, GetMouseWorldPosition());

            // RELOAD LOGIC
            if (Keyboard.current.rKey.wasPressedThisFrame) AttemptReload(ActiveWeaponAbility);
        }

        private void HandleAimingAndExecution()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                // Ignore the click if the mouse is hovering over ANY UI element
                if (EventSystem.current is not null && EventSystem.current.IsPointerOverGameObject())
                    return;

            Vector3? mouseTarget = GetMouseWorldPosition();
            bool isExploration = GameModeManager.Instance.CurrentMode == GameMode.Exploration;

            AbilityContext context = new()
            {
                Caster = gameObject,
                CasterPosition = transform.position,
                MouseWorldPosition = mouseTarget,
                Visualizer = actionVisualizer
            };

            if (!isExploration)
            {
                // Only draw previews and force rotation in combat mode
                movementAbility.DrawPreview(context);
                ActiveWeaponAbility?.DrawPreview(context);

                if (mouseTarget.HasValue) physicsController.LookAtTarget(mouseTarget.Value);
            }

            // --- INPUT ROUTING ---
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (isExploration)
                {
                    // BYPASS ABILITY SYSTEM: 
                    // Feed the raw, unclamped mouse click directly to the NavMeshAgent.
                    if (mouseTarget.HasValue) physicsController.StartMovement(mouseTarget.Value);
                }
                else
                {
                    // TACTICAL COMBAT:
                    // Route through AttemptAbility to apply distance clamping and Turn/Oxygen costs.
                    AttemptAbility(movementAbility, mouseTarget);
                }
            }
            else if (Mouse.current.rightButton.wasPressedThisFrame && !isExploration)
            {
                // Auto-reload fallback if the player tries to shoot while empty
                if (ActiveWeaponAbility is IWeaponAbility weapon && weapon.NeedsReload)
                {
                    Debug.Log("Weapon empty! Auto-reloading...");
                    AttemptReload(ActiveWeaponAbility);
                }
                else
                {
                    AttemptAbility(ActiveWeaponAbility, mouseTarget);
                }
            }
        }

        private void AttemptReload(IAbility ability)
        {
            if (ability is IWeaponAbility weapon)
            {
                if (weapon.CurrentAmmo >= weapon.MaxAmmo)
                {
                    Debug.Log("Magazine already full.");
                    return;
                }

                // Execute the reload
                weapon.Reload();

                // Consume the turns required to reload
                TurnManager.Instance.ExecuteTurns(weapon.ReloadTurnCost);
            }
        }

        private void AttemptAbility(IAbility ability, Vector3? targetPos)
        {
            if (ability.RequiresTargeting && !targetPos.HasValue)
            {
                Debug.LogWarning(
                    $"[Movement Debug] {ability.AbilityId} aborted: targetPos is null. The mouse raycast did not hit the Floor Mask!");
                return;
            }

            AbilityContext context = new()
            {
                Caster = gameObject,
                CasterPosition = transform.position,
                MouseWorldPosition = targetPos,
                Visualizer = actionVisualizer
            };

            if (!ability.CanExecute(context))
            {
                Debug.LogWarning($"[Movement Debug] {ability.AbilityId} cannot execute (CanExecute returned false).");
                return;
            }

            bool isExploration = GameModeManager.Instance.CurrentMode == GameMode.Exploration;

            if (oxygen.TryConsume(ability.OxygenCost))
            {
                actionVisualizer?.Hide();
                ability.Execute(context);

                Debug.Log($"[Movement Debug] Successfully executed {ability.AbilityId} at {targetPos.Value}");

                if (!isExploration) TurnManager.Instance.ExecuteTurns(ability.TurnCost);
            }
            else
            {
                Debug.LogWarning(
                    $"[Movement Debug] Not enough oxygen for {ability.AbilityId}! (Cost: {ability.OxygenCost})");
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