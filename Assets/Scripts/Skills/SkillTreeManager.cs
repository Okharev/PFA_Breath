using System;
using System.Collections.Generic;
using UnityEngine;

namespace Skills
{
    public class SkillTreeManager : MonoBehaviour
    {
        [Header("Player Currencies")] public int genericPoints;

        public readonly Dictionary<EmotionType, int> emotionPoints = new();

        // O(1) lookup. When evaluating a tree of 100 nodes, HashSets prevent massive performance drops.
        private readonly HashSet<string> unlockedNodeGuids = new();
        public static SkillTreeManager Instance { get; private set; }

        private void Awake()
        {
            if (!Instance) Instance = this;
            else Destroy(gameObject);
            // Initialize dictionary to prevent KeyNotFound exceptions
            foreach (EmotionType emotion in Enum.GetValues(typeof(EmotionType))) emotionPoints[emotion] = 0;
        }


        public bool CanUnlock(SkillNodeData node)
        {
            if (unlockedNodeGuids.Contains(node.GUID)) return false; // Already bought

            // Check Prerequisites using the GUID strings saved by our GraphView wires
            foreach (string reqGuid in node.PrerequisiteGUIDs)
                if (!unlockedNodeGuids.Contains(reqGuid))
                    return false;

            // Check Costs
            if (genericPoints < node.Cost.GenericPoints) return false;
            if (node.Cost.EmotionPoints > 0 &&
                emotionPoints[node.Cost.RequiredEmotion] < node.Cost.EmotionPoints) return false;

            return true;
        }

        public bool TryUnlock(SkillNodeData node)
        {
            if (!CanUnlock(node)) return false;

            // 1. Deduct currencies
            genericPoints -= node.Cost.GenericPoints;
            if (node.Cost.EmotionPoints > 0)
                emotionPoints[node.Cost.RequiredEmotion] -= node.Cost.EmotionPoints;

            // 2. Apply Stats via the Modifier Pattern
            PlayerStats playerStats = FindAnyObjectByType<PlayerStats>();
            foreach (StatModifierData mod in node.GrantedStats)
            {
                StatModifierData initializedMod = mod;
                initializedMod.Source = node; // Tag the source in case we implement a "Refund Node" feature
                playerStats.GetStat(mod.Stat).AddModifier(initializedMod);
            }

            // 3. Mark as unlocked
            unlockedNodeGuids.Add(node.GUID);
            Debug.Log($"Unlocked Skill: {node.NodeName}");

            return true;
        }

        public bool IsUnlocked(SkillNodeData node)
        {
            return unlockedNodeGuids.Contains(node.GUID);
        }
    }
}