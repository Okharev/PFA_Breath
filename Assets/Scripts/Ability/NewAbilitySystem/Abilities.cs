using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Ability.NewAbilitySystem
{
    [Serializable]
    public class MaxRangeCondition : IAbilityCondition
    {
        public float maxRange = 10f;

        public bool CanExecute(AbilityContext context)
        {
            float distance = Vector3.Distance(context.Source.transform.position, context.TargetPosition);
            return distance <= maxRange;
        }
    }
    
    [Serializable]
    public class RecallProjectilesEffect : IAbilityEffect
    {
        public void Execute(AbilityContext context)
        {
            // Ask the manager for all projectiles fired by this specific unit
            // var activeProjectiles = ProjectileManager.Instance.GetProjectilesBySource(context.Source);
            // 
            
            // foreach (var projectile in activeProjectiles)
            // {
            //     // Assuming your ProjectileComponent has a method to reverse its target
            //     projectile.SetHomingTarget(context.Source.transform);
            // }
        }
    }

    [Serializable]
    public class ReloadEffect : IAbilityEffect
    {
        public void Execute(AbilityContext context)
        {
            if (context.Source.TryGetComponent(out AmmoComponent ammo))
            {
                ammo.Reload();
            }
        }
    }
    
    [Serializable]
    public class ConsumeAmmoEffect : IAbilityEffect
    {
        [Tooltip("How many bullets are consumed per cast?")]
        public int ammoCost = 1;

        public void Execute(AbilityContext context)
        {
            if (context.Source.TryGetComponent(out AmmoComponent ammo))
            {
                ammo.Consume(ammoCost);
            }
        }
    }
    
    [Serializable]
    public class SpawnProjectileEffect : IAbilityEffect
    {
        public GameObject projectilePrefab;
        public int count = 1;
        [Tooltip("The total arc in degrees. E.g., 30 means +/- 15 degrees from the aim vector.")]
        public float coneAngle = 30f;
        [Tooltip("Offset from the forward aim direction. 0 is forward, 90 is right, -90 is left.")]
        public float baseAngleOffset = 0f;

        public void Execute(AbilityContext context)
        {
            Vector3 origin = context.Source.transform.position;
            Vector3 aimDirection = (context.TargetPosition - origin).normalized;

            // Apply the base offset (e.g., to shoot out of the sides)
            aimDirection = Quaternion.Euler(0, baseAngleOffset, 0) * aimDirection;

            for (int i = 0; i < count; i++)
            {
                // Calculate a random angle within the cone (-half angle to +half angle)
                float randomAngle = Random.Range(-coneAngle / 2f, coneAngle / 2f);
                Vector3 finalDirection = Quaternion.Euler(0, randomAngle, 0) * aimDirection;

                GameObject proj = Object.Instantiate(projectilePrefab, origin, Quaternion.LookRotation(finalDirection));
            
                // Register the projectile so the Ultimate can find it later
                // if (proj.TryGetComponent(out ProjectileComponent p))
                // {
                //     p.Initialize(context.Source, finalDirection);
                //     ProjectileManager.Instance.RegisterProjectile(context.Source, p);
                // }
            }
        }
    }

    [Serializable]
    public class TeleportEffect : IAbilityEffect
    {
        public void Execute(AbilityContext context)
        {
            // For physics-based characters, you might need to use Rigidbody.position
            context.Source.transform.position = context.TargetPosition;
        }
    }
    
    [Serializable]
    public class DamageEffect : IAbilityEffect
    {
        public int damageAmount = 10;

        public void Execute(AbilityContext context)
        {
            if (context.Target != null && context.Target.TryGetComponent(out HealthComponent health))
            {
                health.TakeDamage(damageAmount);
            }
        }
    }

    [Serializable]
    public class MoveEffect : IAbilityEffect
    {
        // Implementation for moving the context.Source to context.TargetPosition
        public void Execute(AbilityContext context)
        {
            // Example: context.Source.transform.position = context.TargetPosition;
        }
    }
}