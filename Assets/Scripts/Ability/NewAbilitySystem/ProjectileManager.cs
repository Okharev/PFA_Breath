using System.Collections.Generic;
using UnityEngine;

namespace Ability.NewAbilitySystem
{
    [DefaultExecutionOrder(-100)]
    public class ProjectileManager : MonoBehaviour
    {
        // Maps a Source (Player/Enemy) to a HashSet of their active projectiles for O(1) lookups
        private readonly Dictionary<GameObject, HashSet<Projectile>> activeProjectiles = new();
        public static ProjectileManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void RegisterProjectile(GameObject source, Projectile projectile)
        {
            if (!activeProjectiles.ContainsKey(source)) activeProjectiles[source] = new HashSet<Projectile>();
            activeProjectiles[source].Add(projectile);
        }

        public void UnregisterProjectile(GameObject source, Projectile projectile)
        {
            if (activeProjectiles.TryGetValue(source, out HashSet<Projectile> activeProjectile))
                activeProjectile.Remove(projectile);
        }

        public HashSet<Projectile> GetProjectilesBySource(GameObject source)
        {
            return activeProjectiles.GetValueOrDefault(source);
        }
    }
}