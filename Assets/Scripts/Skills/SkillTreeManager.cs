using System;
using System.Collections.Generic;
using UnityEngine;
using Skills.Skills;

namespace Skills
{
    public class SkillTreeManager : MonoBehaviour
    {
        [Header("Player Currencies")] 
        public int genericPoints;
        public readonly Dictionary<EmotionType, int> emotionPoints = new();

        // O(1) lookup. HashSets prevent massive performance drops when checking unlock states.
        private readonly HashSet<string> unlockedNodeGuids = new();
        
        public static SkillTreeManager Instance { get; private set; }

        private void Awake()
        {
            if (!Instance) Instance = this;
            else Destroy(gameObject);
            
            // Initialize dictionary to prevent KeyNotFound exceptions
            foreach (EmotionType emotion in Enum.GetValues(typeof(EmotionType))) 
            {
                emotionPoints[emotion] = 0;
            }
        }

        public bool CanUnlock(BaseNodeData node)
        {
            if (unlockedNodeGuids.Contains(node.GUID)) return false; // Already bought

            // 1. Check Prerequisites
            foreach (string reqGuid in node.PrerequisiteGUIDs)
            {
                if (!unlockedNodeGuids.Contains(reqGuid)) return false;
            }

            // 2. Check Costs via Pattern Matching
            return node switch
            {
                GenericNodeData genericNode => genericPoints >= genericNode.GenericCost,
                
                EmotionNodeData emotionNode => emotionPoints[emotionNode.RequiredEmotion] >= emotionNode.BaseEmotionCost,
                
                // Fallback for any future node types that might not have a cost
                _ => true 
            };
        }

        public bool TryUnlock(BaseNodeData node)
        {
            if (!CanUnlock(node)) return false;

            List<StatModifierData> statsToApply = new List<StatModifierData>();

            // 1. Deduct currencies and extract stats based on the node type
            if (node is GenericNodeData genericNode)
            {
                genericPoints -= genericNode.GenericCost;
                statsToApply = genericNode.GrantedStats;
            }
            else if (node is EmotionNodeData emotionNode)
            {
                emotionPoints[emotionNode.RequiredEmotion] -= emotionNode.BaseEmotionCost;
                statsToApply = emotionNode.GrantedStats;
                
                // Here is where you would also hook into your Ability Manager:
                // if (emotionNode.UnlocksAbility) AbilityManager.Unlock(emotionNode.GrantedAbilityId);
            }

            // 2. Apply Stats via the Modifier Pattern
            PlayerStats playerStats = FindAnyObjectByType<PlayerStats>();
            if (playerStats is not null && statsToApply != null)
            {
                foreach (StatModifierData mod in statsToApply)
                {
                    StatModifierData initializedMod = mod;
                    initializedMod.Source = node; // Tag the source in case of respec
                    playerStats.GetStat(mod.Stat).AddModifier(initializedMod);
                }
            }

            // 3. Mark as unlocked
            unlockedNodeGuids.Add(node.GUID);
            Debug.Log($"[SkillTreeManager] Unlocked Skill: {node.NodeName}");

            return true;
        }

        public bool IsUnlocked(BaseNodeData node)
        {
            return unlockedNodeGuids.Contains(node.GUID);
        }
    }
}