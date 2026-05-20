using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Ability
{
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

        public bool CanExecute(AbilityContext context) => true;
        public void DrawPreview(AbilityContext context)
        {
            if (context.MouseWorldPosition.HasValue)
                // Draw a wider intent cone for the shotgun
                context.Visualizer.DrawIntent(firePoint.position, context.MouseWorldPosition.Value,
                    ActionVisualizer.IntentType.Shooting, spreadAngle);
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
        public int OxygenCost => 0; // Or whatever movement costs
        public bool RequiresTargeting => true;

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

    public class SniperAbility : IAbility, IDisposable
    {
        private readonly Transform firePoint;
        private readonly int maxCooldown = 4;

        private readonly GameObject projectilePrefab;

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

        // --- NEW: Validation ---
        public bool CanExecute(AbilityContext context)
        {
            return currentState == SniperState.Ready;
        }

        public void DrawPreview(AbilityContext context)
        {
            if (currentState != SniperState.Ready) return;

            if (context.MouseWorldPosition.HasValue)
                context.Visualizer.DrawIntent(firePoint.position, context.MouseWorldPosition.Value,
                    ActionVisualizer.IntentType.Shooting);
        }

        public void Execute(AbilityContext context)
        {
            // We no longer need to check state here because CanExecute() handles it!
            Vector3 target = context.MouseWorldPosition.Value;
            lockedTargetDirection = (target - firePoint.position).normalized;

            currentState = SniperState.Channeling;
            turnsChanneled = 0;

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
                currentCooldownTurns--;
                if (currentCooldownTurns <= 0)
                {
                    currentState = SniperState.Ready;
                    Debug.Log("Sniper is ready to fire!");
                }

                return;
            }

            if (currentState == SniperState.Channeling)
            {
                turnsChanneled++;

                // We fire the payload when we have 1 turn remaining. 
                // This guarantees the game time remains unpaused long enough for the bullet to fly.
                if (turnsChanneled == Mathf.Max(1, TurnCost - 1)) FirePayload();

                // Once the full TurnCost is met, we transition to Cooldown
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

        private enum SniperState
        {
            Ready,
            Channeling,
            Cooldown
        }
    }


public class TeleportAbility : IAbility
    {
        private readonly float maxTeleportDistance;

        public TeleportAbility(float maxDistance)
        {
            maxTeleportDistance = maxDistance;
        }

        public bool CanExecute(AbilityContext context) => true;

        public string AbilityId => "Blink_Strike";
        public int TurnCost => 0; // Or 1, depending on your previous choice
        public int OxygenCost => 40;
        public bool RequiresTargeting => true;

        public void DrawPreview(AbilityContext context)
        {
            if (context.MouseWorldPosition.HasValue)
            {
                Vector3 targetPos = context.MouseWorldPosition.Value;
                
                // Keep the preview line perfectly level with the player's waist/pivot
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
            
            // Preserve the player's original height so they don't sink into the floor!
            targetPos.y = context.CasterPosition.y;

            Vector3 direction = targetPos - context.CasterPosition;

            if (direction.magnitude > maxTeleportDistance)
                targetPos = context.CasterPosition + direction.normalized * maxTeleportDistance;

            // Snap the transform
            context.Caster.transform.position = targetPos;
            Physics.SyncTransforms();
        }
    }
}