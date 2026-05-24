using System;
using Skills;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Ability
{
    public class TeleportAbility : IAbility, IDisposable
    {
        private readonly float maxTeleportDistance;
        private readonly PlayerController physicsController; // NEW: Added Reference

        public TeleportAbility(PlayerController controller, float maxDistance)
        {
            physicsController = controller;
            maxTeleportDistance = maxDistance;
            TurnManager.OnTurnTicked += HandleTurnTicked;
            CurrentLevel = 1;
        }

        // State tracking

        public int CurrentLevel { get; private set; }

        public void SetLevel(int newLevel)
        {
            CurrentLevel = newLevel;
        }

        public string AbilityId => "Blink_Strike";
        public int TurnCost => 0;
        public int OxygenCost => 40;
        public bool RequiresTargeting => true;

        // --- UI Properties ---
        public int CurrentCooldown { get; private set; }

        public int MaxCooldown { get; } = 3;

        public int CurrentChannelTime => 0;
        public int RequiredChannelTime => 0;
        public bool IsChanneling => false;
        public bool IsReady => CurrentCooldown <= 0;

        public bool CanExecute(AbilityContext context)
        {
            return IsReady;
        }

        public void DrawPreview(AbilityContext context)
        {
            if (!IsReady) return;

            if (context.MouseWorldPosition.HasValue)
            {
                Vector3 targetPos = context.MouseWorldPosition.Value;
                targetPos.y = context.CasterPosition.y;
                Vector3 direction = targetPos - context.CasterPosition;

                if (direction.magnitude > maxTeleportDistance)
                    targetPos = context.CasterPosition + direction.normalized * maxTeleportDistance;

                context.Visualizer.DrawIntent(context.CasterPosition, targetPos, ActionVisualizer.IntentType.Movement);
            }
        }

        public void Execute(AbilityContext context)
        {
            if (!context.MouseWorldPosition.HasValue) return;

            Vector3 targetPos = context.MouseWorldPosition.Value;
            targetPos.y = context.CasterPosition.y;
            Vector3 direction = targetPos - context.CasterPosition;

            if (direction.magnitude > maxTeleportDistance)
                targetPos = context.CasterPosition + direction.normalized * maxTeleportDistance;

            // FIX: Use the safe physics controller method instead of forcing the transform!
            physicsController.TeleportTo(targetPos);

            CurrentCooldown = MaxCooldown;
        }

        public void Dispose()
        {
            TurnManager.OnTurnTicked -= HandleTurnTicked;
        }

        private void HandleTurnTicked(int newTurnNumber)
        {
            if (CurrentCooldown > 0) CurrentCooldown--;
        }
    }


    public class DashAbility : IAbility, IDisposable
    {
        private readonly float maxDashDistance;
        private readonly PlayerController physicsController;

        public DashAbility(PlayerController controller, float maxDistance)
        {
            physicsController = controller;
            maxDashDistance = maxDistance;
            TurnManager.OnTurnTicked += HandleTurnTicked;
            CurrentLevel = 1;
        }

        public int CurrentLevel { get; private set; }

        public void SetLevel(int newLevel)
        {
            CurrentLevel = newLevel;
        }

        public string AbilityId => "Evasive_Dash";
        public int TurnCost => 1; // Costs 1 turn to execute
        public int OxygenCost => 15;
        public bool RequiresTargeting => true;

        // --- UI Properties ---
        public int CurrentCooldown { get; private set; }

        public int MaxCooldown { get; } = 2;

        public int CurrentChannelTime => 0;
        public int RequiredChannelTime => 0;
        public bool IsChanneling => false;
        public bool IsReady => CurrentCooldown <= 0;

        public bool CanExecute(AbilityContext context)
        {
            return IsReady;
        }

        public void DrawPreview(AbilityContext context)
        {
            if (!IsReady || !context.MouseWorldPosition.HasValue) return;

            Vector3 target = GetClampedTarget(context.CasterPosition, context.MouseWorldPosition.Value);
            // Draw the preview. You could add a new IntentType.Dash for a unique color!
            context.Visualizer.DrawIntent(context.CasterPosition, target, ActionVisualizer.IntentType.Movement);
        }

        public void Execute(AbilityContext context)
        {
            if (!context.MouseWorldPosition.HasValue) return;

            Vector3 target = GetClampedTarget(context.CasterPosition, context.MouseWorldPosition.Value);
            physicsController.StartDash(target);

            CurrentCooldown = MaxCooldown;
        }

        public void Dispose()
        {
            TurnManager.OnTurnTicked -= HandleTurnTicked;
        }

        private Vector3 GetClampedTarget(Vector3 start, Vector3 rawTarget)
        {
            Vector3 direction = rawTarget - start;
            direction.y = 0f; // Keep it on the floor plane

            Vector3 clampedTarget = new(rawTarget.x, start.y, rawTarget.z);
            if (direction.magnitude > maxDashDistance) clampedTarget = start + direction.normalized * maxDashDistance;
            return clampedTarget;
        }

        private void HandleTurnTicked(int newTurnNumber)
        {
            if (CurrentCooldown > 0) CurrentCooldown--;
        }
    }


    public class BarrelThroughDashAbility : IAbility, IDisposable
    {
        private readonly float maxDashDistance;
        private readonly PlayerController physicsController;

        public BarrelThroughDashAbility(PlayerController controller, float maxDistance)
        {
            physicsController = controller;
            maxDashDistance = maxDistance;
            TurnManager.OnTurnTicked += HandleTurnTicked;
            CurrentLevel = 1;
        }

        public int CurrentLevel { get; private set; }

        public void SetLevel(int newLevel)
        {
            CurrentLevel = newLevel;
        }

        public string AbilityId => "BarrelThrough_Dash";
        public int TurnCost => 1; // Costs 1 turn to execute
        public int OxygenCost => 15;
        public bool RequiresTargeting => true;

        // --- UI Properties ---
        public int CurrentCooldown { get; private set; }

        public int MaxCooldown { get; } = 2;

        public int CurrentChannelTime => 0;
        public int RequiredChannelTime => 0;
        public bool IsChanneling => false;
        public bool IsReady => CurrentCooldown <= 0;

        public bool CanExecute(AbilityContext context)
        {
            return IsReady;
        }

        public void DrawPreview(AbilityContext context)
        {
            if (!IsReady || !context.MouseWorldPosition.HasValue) return;

            Vector3 target = GetClampedTarget(context.CasterPosition, context.MouseWorldPosition.Value);
            // Draw the preview. You could add a new IntentType.Dash for a unique color!
            context.Visualizer.DrawIntent(context.CasterPosition, target, ActionVisualizer.IntentType.Movement);
        }

        public void Execute(AbilityContext context)
        {
            if (!context.MouseWorldPosition.HasValue) return;

            Vector3 target = GetClampedTarget(context.CasterPosition, context.MouseWorldPosition.Value);
            physicsController.StartDash(target);

            CurrentCooldown = MaxCooldown;
        }

        public void Dispose()
        {
            TurnManager.OnTurnTicked -= HandleTurnTicked;
        }

        private Vector3 GetClampedTarget(Vector3 start, Vector3 rawTarget)
        {
            Vector3 direction = rawTarget - start;
            direction.y = 0f; // Keep it on the floor plane

            Vector3 clampedTarget = new(rawTarget.x, start.y, rawTarget.z);
            if (direction.magnitude > maxDashDistance) clampedTarget = start + direction.normalized * maxDashDistance;
            return clampedTarget;
        }

        private void HandleTurnTicked(int newTurnNumber)
        {
            if (CurrentCooldown > 0) CurrentCooldown--;
        }
    }

    public class SniperAbility : IAbility, IDisposable
    {
        private readonly Transform firePoint;
        private readonly GameObject projectilePrefab;

        private SniperState currentState = SniperState.Ready;
        private Vector3 lockedTargetDirection;

        public SniperAbility(GameObject prefab, Transform firePoint)
        {
            projectilePrefab = prefab;
            this.firePoint = firePoint;
            TurnManager.OnTurnTicked += HandleTurnTicked;
            CurrentLevel = 1;
        }

        public int CurrentLevel { get; private set; }

        public void SetLevel(int newLevel)
        {
            CurrentLevel = newLevel;
        }

        public string AbilityId => "Railgun_Sniper_Channeled";
        public int TurnCost => 2;
        public int OxygenCost => 30;
        public bool RequiresTargeting => true;

        // --- UI Properties ---
        public int CurrentCooldown { get; private set; }

        public int MaxCooldown { get; } = 4;

        public int CurrentChannelTime { get; private set; }

        public int RequiredChannelTime => TurnCost;
        public bool IsChanneling => currentState == SniperState.Channeling;
        public bool IsReady => currentState == SniperState.Ready;

        public bool CanExecute(AbilityContext context)
        {
            return IsReady;
        }

        public void DrawPreview(AbilityContext context)
        {
            if (!IsReady) return;

            if (context.MouseWorldPosition.HasValue)
                context.Visualizer.DrawIntent(firePoint.position, context.MouseWorldPosition.Value,
                    ActionVisualizer.IntentType.Shooting);
        }

        public void Execute(AbilityContext context)
        {
            Vector3 target = context.MouseWorldPosition.Value;
            lockedTargetDirection = (target - firePoint.position).normalized;

            currentState = SniperState.Channeling;
            CurrentChannelTime = 0;
            Debug.Log("Sniper is charging! (Player is locked)");
        }

        public void Dispose()
        {
            TurnManager.OnTurnTicked -= HandleTurnTicked;
        }

        private void HandleTurnTicked(int newTurnNumber)
        {
            if (currentState == SniperState.Cooldown)
            {
                CurrentCooldown--;
                if (CurrentCooldown <= 0) currentState = SniperState.Ready;
                return;
            }

            if (currentState == SniperState.Channeling)
            {
                CurrentChannelTime++;

                if (CurrentChannelTime == Mathf.Max(1, TurnCost - 1)) FirePayload();

                if (CurrentChannelTime >= TurnCost)
                {
                    currentState = SniperState.Cooldown;
                    CurrentCooldown = MaxCooldown;
                }
            }
        }

        private void FirePayload()
        {
            Debug.Log("Sniper fired!");
            Object.Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(lockedTargetDirection));
        }

        private enum SniperState
        {
            Ready,
            Channeling,
            Cooldown
        }
    }

    public class MovementAbility : IAbility
    {
        private readonly float maxMoveDistance;
        private readonly PlayerController physicsController;

        public MovementAbility(PlayerController controller, float maxDistance)
        {
            physicsController = controller;
            maxMoveDistance = maxDistance;
            CurrentLevel = 1;
        }

        public int CurrentLevel { get; private set; }

        public void SetLevel(int newLevel)
        {
            CurrentLevel = newLevel;
        }

        public string AbilityId => "Basic_Move";
        public int TurnCost => 1;
        public int OxygenCost => 0;
        public bool RequiresTargeting => true;

        // --- Stateless UI Properties ---
        public int CurrentCooldown => 0;
        public int MaxCooldown => 0;
        public int CurrentChannelTime => 0;
        public int RequiredChannelTime => 0;
        public bool IsChanneling => false;
        public bool IsReady => true;

        public bool CanExecute(AbilityContext context)
        {
            return true;
        }

        public void DrawPreview(AbilityContext context)
        {
            if (context.MouseWorldPosition.HasValue)
            {
                Vector3 target = GetClampedTarget(context.CasterPosition, context.MouseWorldPosition.Value);
                context.Visualizer.DrawIntent(context.CasterPosition, target, ActionVisualizer.IntentType.Movement);
            }
        }

        public void Execute(AbilityContext context)
        {
            if (context.MouseWorldPosition.HasValue)
            {
                Vector3 target = GetClampedTarget(context.CasterPosition, context.MouseWorldPosition.Value);
                physicsController.StartMovement(target);
            }
        }

        private Vector3 GetClampedTarget(Vector3 start, Vector3 rawTarget)
        {
            Vector3 direction = rawTarget - start;
            direction.y = 0f;

            Vector3 clampedTarget = new(rawTarget.x, start.y, rawTarget.z);
            if (direction.magnitude > maxMoveDistance) clampedTarget = start + direction.normalized * maxMoveDistance;
            return clampedTarget;
        }
    }

// In Abilities.cs
    public class ShotgunAbility : IWeaponAbility
    {
        private readonly Transform firePoint;
        private readonly GameObject pelletPrefab;
        private readonly PlayerStats playerStats; // Injected Dependency

        public ShotgunAbility(GameObject prefab, Transform firePoint, PlayerStats stats)
        {
            pelletPrefab = prefab;
            this.firePoint = firePoint;
            playerStats = stats;

            CurrentLevel = 1;
            Reload(); // Initialize with a full magazine
        }

        public int CurrentLevel { get; private set; }

        public void SetLevel(int newLevel)
        {
            CurrentLevel = newLevel;
        }

        public string AbilityId => "Shotgun_Blast";
        public int TurnCost => 1;

        // Dynamic Oxygen Cost factoring in the reduction stat
        public int OxygenCost => Mathf.Max(0, 20 - (int)playerStats.GetStatValue(StatType.OxygenCostReduction));

        public bool RequiresTargeting => true;

        // --- Stateless UI Properties ---
        public int CurrentCooldown => 0;
        public int MaxCooldown => 0;
        public int CurrentChannelTime => 0;
        public int RequiredChannelTime => 0;
        public bool IsChanneling => false;

        // The ability is only ready to fire if it doesn't need a reload
        public bool IsReady => !NeedsReload;

        // --- IWeaponAbility Implementation ---
        public int CurrentAmmo { get; private set; }
        public int MaxAmmo => (int)playerStats.GetStatValue(StatType.MaxAmmo);
        public int ReloadTurnCost => (int)playerStats.GetStatValue(StatType.ReloadTurnCost);
        public bool NeedsReload => CurrentAmmo <= 0;

        public void Reload()
        {
            CurrentAmmo = MaxAmmo;
            Debug.Log($"[{AbilityId}] Reloaded! Ammo: {CurrentAmmo}/{MaxAmmo}");
        }

        public bool CanExecute(AbilityContext context)
        {
            return IsReady;
        }

        public void DrawPreview(AbilityContext context)
        {
            if (context.MouseWorldPosition.HasValue && !NeedsReload)
            {
                float dynamicSpread = playerStats.GetStatValue(StatType.Spread);
                context.Visualizer.DrawIntent(firePoint.position, context.MouseWorldPosition.Value,
                    ActionVisualizer.IntentType.Shooting, dynamicSpread);
            }
        }

        public void Execute(AbilityContext context)
        {
            if (!context.MouseWorldPosition.HasValue || NeedsReload) return;

            // Deduct Ammo
            CurrentAmmo--;

            // Poll O(1) dynamic stats at the exact moment of execution
            int dynamicPellets = (int)playerStats.GetStatValue(StatType.ProjectileCount);
            float dynamicSpread = playerStats.GetStatValue(StatType.Spread);

            Vector3 baseDirection = (context.MouseWorldPosition.Value - firePoint.position).normalized;

            for (int i = 0; i < dynamicPellets; i++)
            {
                float randomYaw = Random.Range(-dynamicSpread, dynamicSpread);
                Quaternion spreadRotation = Quaternion.Euler(0f, randomYaw, 0f);
                Vector3 finalDirection = spreadRotation * baseDirection;

                // TIP: For performance, consider using an Object Pool pattern here instead of Instantiate
                Object.Instantiate(pelletPrefab, firePoint.position, Quaternion.LookRotation(finalDirection));
            }
        }
    }
}