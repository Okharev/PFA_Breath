using UnityEngine;
using UnityEngine.VFX;

namespace Ability.NewAbilitySystem
{
    public interface IProjectileBehavior
    {
        /// <summary>
        ///     Called every frame by the Projectile to calculate movement and collisions.
        /// </summary>
        void UpdateBehavior(Projectile projectile, float deltaTime);
    }

    public class LinearFlightBehavior : IProjectileBehavior
    {
        public void UpdateBehavior(Projectile projectile, float deltaTime)
        {
            float moveDistance = projectile.speed * deltaTime;

            // Use the projectile's cached properties
            if (Physics.Raycast(projectile.transform.position, projectile.transform.forward, out RaycastHit hit,
                    moveDistance, projectile.hitMask))
            {
                projectile.HandleHit(hit.collider.gameObject, hit.point);
                projectile.transform.position = hit.point;
            }
            else
            {
                projectile.transform.position += projectile.transform.forward * moveDistance;
            }
        }
    }

    public class HomingFlightBehavior : IProjectileBehavior
    {
        private readonly Transform target;

        public HomingFlightBehavior(Transform targetTransform, float newSpeed)
        {
            target = targetTransform;
        }

        public void UpdateBehavior(Projectile projectile, float deltaTime)
        {
            if (target == null) return;

            // Steer towards the target
            Vector3 direction = (target.position - projectile.transform.position).normalized;
            if (direction != Vector3.zero) projectile.transform.rotation = Quaternion.LookRotation(direction);

            // If we reached the caster, consume the bullet quietly
            if (Vector3.Distance(projectile.transform.position, target.position) <= projectile.speed * deltaTime)
            {
                Object.Destroy(projectile.gameObject);
                return;
            }

            // Move forward (using the same raycast logic to shred enemies on the way back)
            float moveDistance = projectile.speed * deltaTime;
            if (Physics.Raycast(projectile.transform.position, projectile.transform.forward, out RaycastHit hit,
                    moveDistance, projectile.hitMask))
            {
                projectile.HandleHit(hit.collider.gameObject, hit.point);
                projectile.transform.position = hit.point;
            }
            else
            {
                projectile.transform.position += projectile.transform.forward * moveDistance;
            }
        }
    }

    [RequireComponent(typeof(HazardVolume))]
    public class Projectile : MonoBehaviour
    {
        [Header("Flight Settings")] public float speed = 25f;

        public float maxLifeTime = 5f;
        public LayerMask hitMask;
        
        [Header("VFX")]
        [SerializeField] public GameObject HitVFXPrefab = null;
        
        [Header("Piercing Settings")] public int maxPierces;

        // The active flight strategy
        [SerializeReference]
        private IProjectileBehavior currentBehavior;

        private float lifeTimer;

        // Expose properties for the Behaviors to use
        public int CurrentPierces { get; set; }
        public HazardVolume Payload { get; private set; }

        private void Update()
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer >= maxLifeTime)
            {
                Destroy(gameObject);
                return;
            }

            // Delegate movement entirely to the injected strategy
            currentBehavior?.UpdateBehavior(this, Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (ProjectileManager.Instance != null && Payload != null && Payload.Source != null)
                ProjectileManager.Instance.UnregisterProjectile(Payload.Source, this);
        }

        public void Initialize(GameObject source)
        {
            Payload = GetComponent<HazardVolume>();
            Payload.Source = source;
            Payload.triggerOnPhysicsEnter = false;

            if (ProjectileManager.Instance != null)
                ProjectileManager.Instance.RegisterProjectile(source, this);

            // Default to linear flight when spawned
            SetBehavior(new LinearFlightBehavior());
        }

        // --- THE HOT SWAP ---
        public void SetBehavior(IProjectileBehavior newBehavior)
        {
            currentBehavior = newBehavior;
        }

        public void HandleHit(GameObject hitObject, Vector3 hitPoint)
        {
            if (HitVFXPrefab != null)
            {
                // Instantiate the Prefab which contains the VisualEffect component
                GameObject effectInstance = Instantiate(HitVFXPrefab, hitPoint, hitObject.transform.rotation);
        
                // Note: You must handle destroying the VFX object after it plays.
                // A simple Destroy works for now, assuming the effect lasts less than 2 seconds.
                Destroy(effectInstance, 2f); 
            }
    
            Payload.ApplyTo(hitObject);
            CurrentPierces++;

            if (CurrentPierces > maxPierces) 
            {
                Destroy(gameObject);
            }
        }
    }
}