using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Ability
{
    public class TeleportAbility : IAbility, IDisposable
    {
        private readonly float maxTeleportDistance;
        private readonly PlayerController physicsController; // NEW: Added Reference
        
        // State tracking
        private int currentCooldown;
        private readonly int maxCooldown = 3;

        public TeleportAbility(PlayerController controller, float maxDistance)
        {
            physicsController = controller;
            maxTeleportDistance = maxDistance;
            TurnManager.OnTurnTicked += HandleTurnTicked; 
        }

        public string AbilityId => "Blink_Strike";
        public int TurnCost => 0; 
        public int OxygenCost => 40;
        public bool RequiresTargeting => true;

        // --- UI Properties ---
        public int CurrentCooldown => currentCooldown;
        public int MaxCooldown => maxCooldown;
        public int CurrentChannelTime => 0;
        public int RequiredChannelTime => 0;
        public bool IsChanneling => false;
        public bool IsReady => currentCooldown <= 0;

        public bool CanExecute(AbilityContext context) => IsReady;

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

            currentCooldown = maxCooldown;
        }

        private void HandleTurnTicked(int newTurnNumber)
        {
            if (currentCooldown > 0)
            {
                currentCooldown--;
            }
        }

        public void Dispose()
        {
            TurnManager.OnTurnTicked -= HandleTurnTicked;
        }
    }


    public class DashAbility : IAbility, IDisposable
    {
        private readonly float maxDashDistance;
        private readonly PlayerController physicsController;
        
        private int currentCooldown;
        private readonly int maxCooldown = 2; // Cooldown in turns

        public DashAbility(PlayerController controller, float maxDistance)
        {
            physicsController = controller;
            maxDashDistance = maxDistance;
            TurnManager.OnTurnTicked += HandleTurnTicked;
        }

        public string AbilityId => "Evasive_Dash";
        public int TurnCost => 1; // Costs 1 turn to execute
        public int OxygenCost => 15;
        public bool RequiresTargeting => true;

        // --- UI Properties ---
        public int CurrentCooldown => currentCooldown;
        public int MaxCooldown => maxCooldown;
        public int CurrentChannelTime => 0;
        public int RequiredChannelTime => 0;
        public bool IsChanneling => false;
        public bool IsReady => currentCooldown <= 0;

        public bool CanExecute(AbilityContext context) => IsReady;

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
            
            currentCooldown = maxCooldown;
        }

        private Vector3 GetClampedTarget(Vector3 start, Vector3 rawTarget)
        {
            Vector3 direction = rawTarget - start;
            direction.y = 0f; // Keep it on the floor plane

            Vector3 clampedTarget = new(rawTarget.x, start.y, rawTarget.z);
            if (direction.magnitude > maxDashDistance) 
            {
                clampedTarget = start + direction.normalized * maxDashDistance;
            }
            return clampedTarget;
        }

        private void HandleTurnTicked(int newTurnNumber)
        {
            if (currentCooldown > 0) currentCooldown--;
        }

        public void Dispose()
        {
            TurnManager.OnTurnTicked -= HandleTurnTicked;
        }
    }

    
    public class SniperAbility : IAbility, IDisposable
    {
        private readonly Transform firePoint;
        private readonly GameObject projectilePrefab;
        private readonly int maxCooldown = 4;

        private int currentCooldownTurns;
        private SniperState currentState = SniperState.Ready;
        private Vector3 lockedTargetDirection;
        private int turnsChanneled;

        public SniperAbility(GameObject prefab, Transform firePoint)
        {
            projectilePrefab = prefab;
            this.firePoint = firePoint;
            TurnManager.OnTurnTicked += HandleTurnTicked;
        }

        public string AbilityId => "Railgun_Sniper_Channeled";
        public int TurnCost => 2;
        public int OxygenCost => 30;
        public bool RequiresTargeting => true;

        // --- UI Properties ---
        public int CurrentCooldown => currentCooldownTurns;
        public int MaxCooldown => maxCooldown;
        public int CurrentChannelTime => turnsChanneled;
        public int RequiredChannelTime => TurnCost;
        public bool IsChanneling => currentState == SniperState.Channeling;
        public bool IsReady => currentState == SniperState.Ready;

        public bool CanExecute(AbilityContext context) => IsReady;

        public void DrawPreview(AbilityContext context)
        {
            if (!IsReady) return;

            if (context.MouseWorldPosition.HasValue)
                context.Visualizer.DrawIntent(firePoint.position, context.MouseWorldPosition.Value, ActionVisualizer.IntentType.Shooting);
        }

        public void Execute(AbilityContext context)
        {
            Vector3 target = context.MouseWorldPosition.Value;
            lockedTargetDirection = (target - firePoint.position).normalized;

            currentState = SniperState.Channeling;
            turnsChanneled = 0;
            Debug.Log("Sniper is charging! (Player is locked)");
        }

        private void HandleTurnTicked(int newTurnNumber)
        {
            if (currentState == SniperState.Cooldown)
            {
                currentCooldownTurns--;
                if (currentCooldownTurns <= 0)
                {
                    currentState = SniperState.Ready;
                }
                return;
            }

            if (currentState == SniperState.Channeling)
            {
                turnsChanneled++;

                if (turnsChanneled == Mathf.Max(1, TurnCost - 1)) FirePayload();

                if (turnsChanneled >= TurnCost)
                {
                    currentState = SniperState.Cooldown;
                    currentCooldownTurns = maxCooldown;
                }
            }
        }

        private void FirePayload()
        {
            Debug.Log("Sniper fired!");
            Object.Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(lockedTargetDirection));
        }

        public void Dispose()
        {
            TurnManager.OnTurnTicked -= HandleTurnTicked;
        }

        private enum SniperState { Ready, Channeling, Cooldown }
    }
    
        public class MovementAbility : IAbility
    {
        private readonly float maxMoveDistance;
        private readonly PlayerController physicsController;

        public MovementAbility(PlayerController controller, float maxDistance)
        {
            physicsController = controller;
            maxMoveDistance = maxDistance;
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

        public bool CanExecute(AbilityContext context) => true;

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

    public class ShotgunAbility : IAbility
    {
        private readonly Transform firePoint;
        private readonly int pelletCount;
        private readonly GameObject pelletPrefab;
        private readonly float spreadAngle;

        public ShotgunAbility(GameObject prefab, Transform firePoint, int pelletCount = 5, float spreadAngle = 15f)
        {
            pelletPrefab = prefab;
            this.firePoint = firePoint;
            this.pelletCount = pelletCount;
            this.spreadAngle = spreadAngle;
        }

        public string AbilityId => "Shotgun_Blast";
        public int TurnCost => 1;
        public int OxygenCost => 20;
        public bool RequiresTargeting => true;

        // --- Stateless UI Properties ---
        public int CurrentCooldown => 0;
        public int MaxCooldown => 0;
        public int CurrentChannelTime => 0;
        public int RequiredChannelTime => 0;
        public bool IsChanneling => false;
        public bool IsReady => true;

        public bool CanExecute(AbilityContext context) => true;

        public void DrawPreview(AbilityContext context)
        {
            if (context.MouseWorldPosition.HasValue)
                context.Visualizer.DrawIntent(firePoint.position, context.MouseWorldPosition.Value, ActionVisualizer.IntentType.Shooting, spreadAngle);
        }

        public void Execute(AbilityContext context)
        {
            if (!context.MouseWorldPosition.HasValue) return;

            Vector3 baseDirection = (context.MouseWorldPosition.Value - firePoint.position).normalized;

            for (int i = 0; i < pelletCount; i++)
            {
                float randomYaw = Random.Range(-spreadAngle, spreadAngle);
                Quaternion spreadRotation = Quaternion.Euler(0f, randomYaw, 0f);
                Vector3 finalDirection = spreadRotation * baseDirection;

                Object.Instantiate(pelletPrefab, firePoint.position, Quaternion.LookRotation(finalDirection));
            }
        }
    }
}