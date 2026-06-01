using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Ability.NewAbilitySystem
{
    [Serializable]
    public class MaxRangeCondition : IAbilityCondition
    {
        [HideInInspector] public string name = "Max range condition";

        public float maxRange = 10f;

        public bool CanExecute(AbilityContext context)
        {
            float distance = Vector3.Distance(context.Source.transform.position, context.TargetPosition);
            return distance <= maxRange;
        }
    }

    [Serializable]
    public class OxygenCondition : IAbilityCondition
    {
        [HideInInspector] public string name = "Oxygen Requirement";

        [Tooltip("How much oxygen is required to even attempt casting this?")]
        public float requiredOxygen = 10f;

        public bool CanExecute(AbilityContext context)
        {
            // If the unit breathes oxygen, check their tank
            if (context.Source.TryGetComponent(out OxygenComponent oxygen)) return oxygen.HasOxygen(requiredOxygen);

            // If the unit doesn't have an OxygenComponent (e.g., a Robot enemy), 
            // they cannot cast abilities that strictly require oxygen.
            return false;
        }
    }

    [Serializable]
    public class ConsumeOxygenEffect : IAbilityEffect
    {
        [HideInInspector] public string name = "Consume Oxygen";

        [Tooltip("How much oxygen is burned when this ability successfully fires?")]
        public float oxygenCost = 10f;

        public void Execute(AbilityContext context)
        {
            if (context.Source.TryGetComponent(out OxygenComponent oxygen)) oxygen.Consume(oxygenCost);
        }
    }

    [Serializable]
    public class RecallProjectilesEffect : IAbilityEffect
    {
        [HideInInspector] public string name = "Recall Projectiles";

        public float timeToReturn = 1.0f;

        public void Execute(AbilityContext context)
        {
            if (ProjectileManager.Instance == null) return;

            HashSet<Projectile> activeProjectiles = ProjectileManager.Instance.GetProjectilesBySource(context.Source);
            if (activeProjectiles == null || activeProjectiles.Count == 0) return;

            List<Projectile> projectilesToRecall = new(activeProjectiles);

            foreach (Projectile proj in projectilesToRecall)
            {
                // Calculate required speed
                float distance = Vector3.Distance(proj.transform.position, context.Origin.position);
                proj.speed = distance / timeToReturn;
                proj.maxPierces = 999; // Allow infinite piercing on return

                // Swap the flight behavior!
                proj.SetBehavior(new HomingFlightBehavior(context.Origin, proj.speed));
            }
        }
    }

    [Serializable]
    public class ReloadEffect : IAbilityEffect
    {
        [HideInInspector] public string name = "Reload Effect";

        public void Execute(AbilityContext context)
        {
            if (context.Source.TryGetComponent(out AmmoComponent ammo)) ammo.Reload();
        }
    }

    [Serializable]
    public class MoveEffect : IAbilityEffect
    {
        [HideInInspector] public string name = "Move Effect";

        public void Execute(AbilityContext context)
        {
            // Ensure the source has a NavMeshAgent
            if (context.Source.TryGetComponent(out NavMeshAgent agent))
            {
                // Tell the agent where to go
                agent.SetDestination(context.TargetPosition);
                agent.isStopped = false;
            }
            else
            {
                Debug.LogWarning($"{context.Source.name} is trying to move but has no NavMeshAgent!");
            }
        }
    }

    [Serializable]
    public class ConsumeAmmoEffect : IAbilityEffect
    {
        [HideInInspector] public string name = "Consume Ammo effect";

        [Tooltip("How many bullets are consumed per cast?")]
        public int ammoCost = 1;

        public void Execute(AbilityContext context)
        {
            if (context.Source.TryGetComponent(out AmmoComponent ammo)) ammo.Consume(ammoCost);
        }
    }

    [Serializable]
    public class SpawnLaserHazardEffect : IAbilityEffect, IPreviewableEffect
    {
        [HideInInspector] public string name = "Spawn Laser Hazard";

        [Tooltip("Prefab containing a BoxCollider (IsTrigger), HazardVolume, and TurnBasedLifetime")]
        public GameObject laserHazardPrefab;

        [Tooltip("If true, the laser will be strictly horizontal, ignoring the target's Y elevation.")]
        public bool forcePlanar = true;

        public void Execute(AbilityContext context)
        {
            // Determine origin, falling back to source position if Origin is missing
            Vector3 origin = context.Origin != null ? context.Origin.position : context.Source.transform.position;
            Vector3 target = context.TargetPosition;

            // Flatten the vertical axis to create a strictly horizontal plane
            if (forcePlanar) target.y = origin.y;

            Vector3 direction = target - origin;
            float distance = direction.magnitude;

            // Prevent instantiation errors if the target and origin occupy the exact same spatial coordinate
            if (distance <= Mathf.Epsilon) return;

            // Instantiate at the midpoint between the sniper and the target
            Vector3 spawnPosition = origin + direction / 2f;

            GameObject laser = Object.Instantiate(laserHazardPrefab, spawnPosition, Quaternion.LookRotation(direction));

            // Assuming the laser prefab is a basic 1x1x1 cube geometry, scaling Z stretches it to the target
            laser.transform.localScale = new Vector3(0.2f, 0.2f, distance);

            // Assign the source so the HazardVolume knows who dealt the damage
            if (laser.TryGetComponent(out HazardVolume hazard)) hazard.Source = context.Source;
        }

        public void DrawPreview(AbilityContext context, IntentDrawer drawer)
        {
            Vector3 origin = context.Origin != null ? context.Origin.position : context.Source.transform.position;
            Vector3 target = context.TargetPosition;
            if (forcePlanar) target.y = origin.y;

            // Draw a thick red line
            drawer.DrawLine(origin, target, Color.red);
        }
    }

    [Serializable]
    public class AoEDamageEffect : IAbilityEffect, IPreviewableEffect
    {
        [HideInInspector] public string name = "AoE Damage Effect";

        [Tooltip("Radius of the AoE explosion or pulse.")]
        public float radius = 3f;

        [Tooltip("Amount of damage dealt to targets caught in the blast.")]
        public int damageAmount = 15;

        [Tooltip("Physics layers to check for victims (e.g., Player, Destructibles).")]
        public LayerMask hitMask;

        public void Execute(AbilityContext context)
        {
            // Determine the epicenter of the attack. 
            // We use the Origin (FirePoint) if available, otherwise default to the Source transform.[cite: 3]
            Vector3 epicenter = context.Origin != null ? context.Origin.position : context.Source.transform.position;

            // Perform the highly optimized spatial query
            Collider[] hits = Physics.OverlapSphere(epicenter, radius, hitMask);

            foreach (Collider hit in hits)
            {
                // Optional: Prevent the enemy from damaging itself
                if (hit.gameObject == context.Source) continue;

                // We assume you have a HealthComponent based on your existing DamageEffect implementation[cite: 6]
                if (hit.TryGetComponent(out HealthComponent health))
                {
                    Debug.Log("Did damage to player with aoe");
                    health.TakeDamage(damageAmount);
                }
            }
        }

        public void DrawPreview(AbilityContext context, IntentDrawer drawer)
        {
            Vector3 epicenter = context.Origin != null ? context.Origin.position : context.Source.transform.position;
            // Draw a red danger circle
            drawer.DrawCircle(epicenter, radius, new Color(1f, 0f, 0f, 0.5f));
        }
    }

    [Serializable]
    public class TwinSalvoEffect : IAbilityEffect, IPreviewableEffect // <-- Implemented Interface
    {
        [HideInInspector] public string name = "Twin Salvo Effect";

        [Header("Projectile Settings")] public GameObject projectilePrefab;

        public int burstCount = 2;
        public float burstDuration = 1.0f;
        public bool fireSimultaneously = true;

        [Header("On-Shot Effects")]
        [Tooltip("Triggers exactly when EACH salvo fires (e.g., Consume Ammo, Play Muzzle Flash).")]
        [SerializeReference]
        [SubclassSelector]
        public List<IAbilityEffect> onShotEffects = new();

        [Header("Hierarchy Search (By Name)")] public string leftBarrelName = "LeftBarrel";

        public string rightBarrelName = "RightBarrel";

        [Header("Fallback Settings")] public float fallbackSideAngle = 25f;

        public bool forcePlanar = true;

        [Header("Preview Settings")] [Tooltip("How far the intent lines should be drawn.")]
        public float previewLineLength = 15f;

        public Color previewColor = new(1f, 0.5f, 0f, 0.7f); // Distinct Orange

        public void Execute(AbilityContext context)
        {
            if (burstDuration > 0f && burstCount > 0 && context.Source.TryGetComponent(out MonoBehaviour runner))
                runner.StartCoroutine(FireSalvoRoutine(context));
            else
                FireInstantly(context);
        }

        // --- NEW: Intent Visualization ---
        public void DrawPreview(AbilityContext context, IntentDrawer drawer)
        {
            if (context.Source == null) return;

            // Algorithm Note: Tree traversal yields O(N) time complexity where N is child transforms.
            Transform leftBarrel = FindChildRecursive(context.Source.transform, leftBarrelName);
            Transform rightBarrel = FindChildRecursive(context.Source.transform, rightBarrelName);

            // Path A: Physical Barrels Found
            if (leftBarrel != null && rightBarrel != null)
            {
                drawer.DrawLine(leftBarrel.position, leftBarrel.position + leftBarrel.forward * previewLineLength,
                    previewColor);
                drawer.DrawLine(rightBarrel.position, rightBarrel.position + rightBarrel.forward * previewLineLength,
                    previewColor);
            }
            // Path B: Fallback Mathematics
            else
            {
                Vector3 origin = context.Origin != null ? context.Origin.position : context.Source.transform.position;
                Vector3 aimTarget = context.TargetPosition;

                if (forcePlanar) aimTarget.y = origin.y;

                Vector3 baseAimDirection = (aimTarget - origin).normalized;

                // O(1) Vector Rotations
                Vector3 leftDirection = Quaternion.Euler(0, -fallbackSideAngle, 0) * baseAimDirection;
                Vector3 rightDirection = Quaternion.Euler(0, fallbackSideAngle, 0) * baseAimDirection;

                drawer.DrawLine(origin, origin + leftDirection * previewLineLength, previewColor);
                drawer.DrawLine(origin, origin + rightDirection * previewLineLength, previewColor);
            }
        }

        // --- EXECUTION LOGIC ---

        private IEnumerator FireSalvoRoutine(AbilityContext context)
        {
            float timeBetweenShots = burstDuration / burstCount;
            Transform leftBarrel = FindChildRecursive(context.Source.transform, leftBarrelName);
            Transform rightBarrel = FindChildRecursive(context.Source.transform, rightBarrelName);

            for (int i = 0; i < burstCount; i++)
            {
                if (context.Source == null) yield break;

                // 1. Trigger Per-Shot Effects
                foreach (IAbilityEffect effect in onShotEffects) effect?.Execute(context);

                // 2. Spawn Bullets
                if (leftBarrel != null && rightBarrel != null)
                {
                    if (fireSimultaneously)
                    {
                        SpawnBullet(context, leftBarrel.position, leftBarrel.forward);
                        SpawnBullet(context, rightBarrel.position, rightBarrel.forward);
                    }
                    else
                    {
                        Transform currentBarrel = i % 2 == 0 ? leftBarrel : rightBarrel;
                        SpawnBullet(context, currentBarrel.position, currentBarrel.forward);
                    }
                }
                else
                {
                    FireWithMathOffsets(context, i);
                }

                yield return new WaitForSeconds(timeBetweenShots);
            }
        }

        private void FireInstantly(AbilityContext context)
        {
            Transform leftBarrel = FindChildRecursive(context.Source.transform, leftBarrelName);
            Transform rightBarrel = FindChildRecursive(context.Source.transform, rightBarrelName);

            for (int i = 0; i < burstCount; i++)
            {
                foreach (IAbilityEffect effect in onShotEffects) effect?.Execute(context);

                if (leftBarrel != null && rightBarrel != null)
                {
                    if (fireSimultaneously)
                    {
                        SpawnBullet(context, leftBarrel.position, leftBarrel.forward);
                        SpawnBullet(context, rightBarrel.position, rightBarrel.forward);
                    }
                    else
                    {
                        Transform currentBarrel = i % 2 == 0 ? leftBarrel : rightBarrel;
                        SpawnBullet(context, currentBarrel.position, currentBarrel.forward);
                    }
                }
                else
                {
                    FireWithMathOffsets(context, i);
                }
            }
        }

        // --- HELPER METHODS ---

        private Transform FindChildRecursive(Transform parent, string targetName)
        {
            if (parent.name == targetName) return parent;
            foreach (Transform child in parent)
            {
                Transform result = FindChildRecursive(child, targetName);
                if (result != null) return result;
            }

            return null;
        }

        private void FireWithMathOffsets(AbilityContext context, int iteration)
        {
            Vector3 aimTarget = context.TargetPosition;
            if (forcePlanar) aimTarget.y = context.Origin.position.y;
            Vector3 baseAimDirection = (aimTarget - context.Origin.position).normalized;

            Vector3 leftDirection = Quaternion.Euler(0, -fallbackSideAngle, 0) * baseAimDirection;
            Vector3 rightDirection = Quaternion.Euler(0, fallbackSideAngle, 0) * baseAimDirection;
            Vector3 spawnPosition = context.Origin.position;

            if (fireSimultaneously)
            {
                SpawnBullet(context, spawnPosition, leftDirection);
                SpawnBullet(context, spawnPosition, rightDirection);
            }
            else
            {
                Vector3 currentDirection = iteration % 2 == 0 ? leftDirection : rightDirection;
                SpawnBullet(context, spawnPosition, currentDirection);
            }
        }

        private void SpawnBullet(AbilityContext context, Vector3 position, Vector3 direction)
        {
            GameObject proj = Object.Instantiate(projectilePrefab, position, Quaternion.LookRotation(direction));
            if (proj.TryGetComponent(out Projectile p)) p.Initialize(context.Source);
        }
    }

    [Serializable]
    public class SpawnProjectileEffect : IAbilityEffect, IPreviewableEffect
    {
        [HideInInspector] public string name = "Projectile Effect";

        [Header("Projectile Settings")] public GameObject projectilePrefab;

        public int count = 3;

        [Header("Trajectory & Spread")] public float coneAngle = 30f;

        public float baseAngleOffset;
        public bool forcePlanar = true;

        [Header("Burst Settings")] public float burstDuration = 1.0f;

        [Header("On-Shot Effects")]
        [Tooltip("Triggers exactly when EACH bullet fires (e.g., Consume Ammo, Play Muzzle Flash).")]
        [SerializeReference]
        [SubclassSelector]
        public List<IAbilityEffect> onShotEffects = new();

        public void Execute(AbilityContext context)
        {
            if (burstDuration > 0f && count > 1 && context.Source.TryGetComponent(out MonoBehaviour runner))
                runner.StartCoroutine(FireBurstRoutine(context));
            else
                FireInstantly(context);
        }

        public void DrawPreview(AbilityContext context, IntentDrawer drawer)
        {
            Vector3 origin = context.Origin.position;
            Vector3 aimTarget = context.TargetPosition;
            if (forcePlanar) aimTarget.y = origin.y;

            Vector3 direction = (aimTarget - origin).normalized;
            direction = Quaternion.Euler(0, baseAngleOffset, 0) * direction;

            // Draw an orange cone showing the spread footprint
            drawer.DrawCone(origin, direction, coneAngle, 15f, new Color(1f, 0.5f, 0f, 0.5f));
        }

        private IEnumerator FireBurstRoutine(AbilityContext context)
        {
            float timeBetweenShots = burstDuration / count;

            Vector3 aimTarget = context.TargetPosition;
            if (forcePlanar) aimTarget.y = context.Origin.position.y;
            Vector3 baseAimDirection = (aimTarget - context.Origin.position).normalized;
            baseAimDirection = Quaternion.Euler(0, baseAngleOffset, 0) * baseAimDirection;

            float sectorAngle = coneAngle / count;
            float startingAngle = -coneAngle / 2f;

            for (int i = 0; i < count; i++)
            {
                if (context.Source == null || context.Origin == null) yield break;

                // 1. Trigger Per-Shot Effects (Ammo, Audio, VFX)
                foreach (IAbilityEffect effect in onShotEffects) effect?.Execute(context);

                // 2. Calculate Spread and Spawn
                Vector3 spawnPosition = context.Origin.position;
                float minAngle = startingAngle + i * sectorAngle;
                float maxAngle = minAngle + sectorAngle;
                float finalAngle = Random.Range(minAngle, maxAngle);

                Vector3 finalDirection = Quaternion.Euler(0, finalAngle, 0) * baseAimDirection;

                GameObject proj = Object.Instantiate(projectilePrefab, spawnPosition,
                    Quaternion.LookRotation(finalDirection));
                if (proj.TryGetComponent(out Projectile p)) p.Initialize(context.Source);

                yield return new WaitForSeconds(timeBetweenShots);
            }
        }

        private void FireInstantly(AbilityContext context)
        {
            Vector3 spawnPosition = context.Origin.position;
            Vector3 aimTarget = context.TargetPosition;
            if (forcePlanar) aimTarget.y = spawnPosition.y;

            Vector3 aimDirection = (aimTarget - spawnPosition).normalized;
            aimDirection = Quaternion.Euler(0, baseAngleOffset, 0) * aimDirection;

            float sectorAngle = coneAngle / count;
            float startingAngle = -coneAngle / 2f;

            for (int i = 0; i < count; i++)
            {
                // Trigger Per-Shot Effects
                foreach (IAbilityEffect effect in onShotEffects) effect?.Execute(context);

                float finalAngle = 0f;
                if (count > 1)
                {
                    float minAngle = startingAngle + i * sectorAngle;
                    float maxAngle = minAngle + sectorAngle;
                    finalAngle = Random.Range(minAngle, maxAngle);
                }

                Vector3 finalDirection = Quaternion.Euler(0, finalAngle, 0) * aimDirection;
                GameObject proj = Object.Instantiate(projectilePrefab, spawnPosition,
                    Quaternion.LookRotation(finalDirection));

                if (proj.TryGetComponent(out Projectile p)) p.Initialize(context.Source);
            }
        }
    }
    
    [Serializable]
    public class SafeLandingCondition : IAbilityCondition
    {
        [HideInInspector] public string name = "Safe Landing Check";
        
        [Tooltip("How far down we check for valid ground.")]
        public float dropCheckTolerance = 2.0f;

        public bool CanExecute(AbilityContext context)
        {
            // Predict the end point based on max dash distance
            Vector3 startPos = context.Source.transform.position;
            Vector3 direction = (context.TargetPosition - startPos).normalized;
            
            // Assuming max distance is 5, you might want to pull this from the effect or hardcode a max sync
            float dist = Mathf.Min(Vector3.Distance(startPos, context.TargetPosition), 5f);
            Vector3 predictedEndPos = startPos + (direction * dist);

            // Check if there is valid NavMesh data at the landing zone
            return NavMesh.SamplePosition(predictedEndPos, out _, dropCheckTolerance, NavMesh.AllAreas);
        }
    }
    
    [Serializable]
    public class DashEffect : IAbilityEffect, IPreviewableEffect
    {
        [HideInInspector] public string name = "Dash Phasing Effect";

        [Tooltip("Time in seconds the dash takes to complete. Must be less than TurnManager.secondsPerTurn!")]
        public float dashDuration = 0.25f;
        
        [Tooltip("The maximum distance the player can dash.")]
        public float maxDashDistance = 5f;

        [Tooltip("The physics layer string that ignores Enemies and Projectiles.")]
        public string ghostLayerName = "PhasingGhost";

        public void Execute(AbilityContext context)
        {
            if (context.Source.TryGetComponent(out MonoBehaviour runner))
            {
                runner.StartCoroutine(DashRoutine(context));
            }
        }

        private IEnumerator DashRoutine(AbilityContext context)
        {
            GameObject source = context.Source;
            NavMeshAgent agent = source.GetComponent<NavMeshAgent>();
            
            // Assume you have a HealthComponent. We flag it to ignore damage calculations.
            HealthComponent health = source.GetComponent<HealthComponent>(); 

            // 1. Setup: Disable Agent to cross voids and swap to Ghost Layer
            if (agent != null) agent.enabled = false;
            if (health != null) health.IsInvincible = true;

            int originalLayer = source.layer;
            source.layer = LayerMask.NameToLayer(ghostLayerName);

            // 2. Calculate clamped destination
            Vector3 startPos = source.transform.position;
            Vector3 direction = (context.TargetPosition - startPos).normalized;
            float requestedDistance = Vector3.Distance(startPos, context.TargetPosition);
            float actualDistance = Mathf.Min(requestedDistance, maxDashDistance);
            
            Vector3 endPos = startPos + (direction * actualDistance);

            // 3. Execute Movement
            float elapsed = 0f;
            while (elapsed < dashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dashDuration);
                
                // Using an ease-out curve makes the dash feel punchier
                float easedT = 1f - Mathf.Pow(1f - t, 3f); 
                
                source.transform.position = Vector3.Lerp(startPos, endPos, easedT);
                yield return null;
            }

            source.transform.position = endPos;

            // 4. Cleanup: Re-enable Agent securely on the nearest NavMesh
            if (agent != null)
            {
                // Ensure we don't re-enable the agent over a bottomless pit
                if (NavMesh.SamplePosition(endPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    source.transform.position = hit.position;
                }
                agent.enabled = true;
            }

            if (health != null) health.IsInvincible = false;
            source.layer = originalLayer;
        }

        public void DrawPreview(AbilityContext context, IntentDrawer drawer)
        {
            Vector3 start = context.Source.transform.position;
            Vector3 direction = (context.TargetPosition - start).normalized;
            float dist = Mathf.Min(Vector3.Distance(start, context.TargetPosition), maxDashDistance);
            
            // Draw a bright cyan line to indicate a safe utility dash
            drawer.DrawLine(start, start + (direction * dist), Color.cyan);
        }
    }

    [Serializable]
    public class TeleportEffect : IAbilityEffect
    {
        [HideInInspector] public string name = "Teleport Effect";

        public void Execute(AbilityContext context)
        {
            // For physics-based characters, you might need to use Rigidbody.position
            context.Source.transform.position = context.TargetPosition;
        }
    }

    [Serializable]
    public class DamageEffect : IAbilityEffect
    {
        [HideInInspector] public string name = "Damage Effect";

        public int damageAmount = 10;

        public void Execute(AbilityContext context)
        {
            // Now, if this is called by a HazardVolume, context.Target is the victim!
            if (context.Target != null && context.Target.TryGetComponent(out HealthComponent health))
                health.TakeDamage(damageAmount);
        }
    }
}