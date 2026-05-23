using System.Collections.Generic;
using UnityEngine;

namespace Skills
{
    public class PlayerStats : MonoBehaviour
    {
        // O(1) lookup map
        private Dictionary<StatType, Stat> statMap;

        private void Awake()
        {
            statMap = new Dictionary<StatType, Stat>
            {
                { StatType.Damage, new Stat(10f) },
                { StatType.ProjectileCount, new Stat(1f) }, // Default 1 projectile
                { StatType.OxygenCostReduction, new Stat(0f) }, // Default 0% reduction
                { StatType.Spread, new Stat(15f) },
                { StatType.MovementSpeed, new Stat(5f) }
            };
        }

        public Stat GetStat(StatType type)
        {
            if (statMap.TryGetValue(type, out Stat stat)) return stat;

            Debug.LogError($"Stat {type} not found!");
            return null;
        }

        // Helper method for quick value retrieval
        public float GetStatValue(StatType type)
        {
            return GetStat(type)?.Value ?? 0f;
        }
    }
}