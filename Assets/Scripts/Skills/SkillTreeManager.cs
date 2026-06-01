using System;
using System.Collections.Generic;
using Ability.NewAbilitySystem;
using Skills.Skills;
using UnityEngine;

namespace Skills
{
    public class SkillTreeManager : MonoBehaviour
    {
        [Header("Player Currencies")] 
        public int genericPoints;
        public readonly Dictionary<EmotionType, int> emotionPoints = new();
        private readonly Dictionary<AbilitySlot, string> equippedNodes = new();
        private readonly Dictionary<string, int> nodeLevels = new();

        public static SkillTreeManager Instance { get; private set; }

        // --- NEW: EVENT-DRIVEN DECOUPLING ---
        public static event Action OnSkillTreeUpdated;
        
        // Broadcasts the newly equipped AbilityData, its slot, and its current level
        public static event Action<AbilityData, AbilitySlot, int> OnAbilityEquipped;
        
        // Broadcasts which slot was just emptied
        public static event Action<AbilitySlot> OnAbilityUnequipped;

        private void Awake()
        {
            if (!Instance) Instance = this;
            else { Destroy(gameObject); return; }

            foreach (EmotionType emotion in Enum.GetValues(typeof(EmotionType))) emotionPoints[emotion] = 0;

            AddEmotionPoints(EmotionType.Red, 5);
            AddEmotionPoints(EmotionType.Blue, 15);
            AddEmotionPoints(EmotionType.White, 5);
            AddGenericPoints(8);
        }


        public bool MeetsPrerequisites(BaseNodeData node)
        {
            // 1. Root nodes (no prerequisites) are inherently available to purchase
            if (node.PrerequisiteGUIDs == null || node.PrerequisiteGUIDs.Count == 0)
                return true;

            // 2. The "AND" Gate Validation
            foreach (string reqGuid in node.PrerequisiteGUIDs)
            {
                // If ANY required node has a level of 0 (locked), the validation fails
                if (GetNodeLevel(reqGuid) <= 0) 
                {
                    return false; 
                }
            }

            // 3. If we survived the loop, all prerequisites are met!
            return true;
        }

        public void EquipNode(EmotionNodeData node)
        {
            if (GetNodeLevel(node.GUID) <= 0 || !node.UnlocksAbility || node.GrantedAbility == null) return;

            // 1. Update the local UI state
            equippedNodes[node.IntendedSlot] = node.GUID;

            // 2. Broadcast the change to the rest of the game (No more FindObjectOfType!)
            OnAbilityEquipped?.Invoke(node.GrantedAbility, node.IntendedSlot, GetNodeLevel(node.GUID));

            Debug.Log($"[SkillTreeManager] Equipped {node.GrantedAbility.abilityName} to {node.IntendedSlot} slot.");
            OnSkillTreeUpdated?.Invoke();
        }

        public void ToggleEquipNode(EmotionNodeData node)
        {
            if (GetNodeLevel(node.GUID) <= 0 || !node.UnlocksAbility || node.GrantedAbility == null) return;

            bool isCurrentlyEquipped = IsNodeEquipped(node);

            if (isCurrentlyEquipped)
            {
                equippedNodes.Remove(node.IntendedSlot);
                
                // Broadcast Unequip
                OnAbilityUnequipped?.Invoke(node.IntendedSlot);
                Debug.Log($"[SkillTreeManager] Unequipped {node.GrantedAbility.abilityName}.");
            }
            else
            {
                equippedNodes[node.IntendedSlot] = node.GUID;
                
                // Broadcast Equip
                OnAbilityEquipped?.Invoke(node.GrantedAbility, node.IntendedSlot, GetNodeLevel(node.GUID));
                Debug.Log($"[SkillTreeManager] Equipped {node.GrantedAbility.abilityName} to {node.IntendedSlot} slot.");
            }

            OnSkillTreeUpdated?.Invoke();
        }

        public bool IsNodeEquipped(BaseNodeData node)
        {
            if (node is EmotionNodeData eNode)
                return equippedNodes.TryGetValue(eNode.IntendedSlot, out string equippedGuid) &&
                       equippedGuid == eNode.GUID;
            return false;
        }


        public void AddGenericPoints(int amount)
        {
            genericPoints += amount;
            OnSkillTreeUpdated?.Invoke();
        }

        public void AddEmotionPoints(EmotionType type, int amount)
        {
            if (emotionPoints.ContainsKey(type))
            {
                emotionPoints[type] += amount;
                OnSkillTreeUpdated?.Invoke();
            }
        }

        // Helper method to pull levels safely
        public int GetNodeLevel(string guid)
        {
            return nodeLevels.TryGetValue(guid, out int level) ? level : 0;
        }

        // Helper method to centralize and scale costs
        public int GetNodeCost(BaseNodeData node)
        {
            int currentLevel = GetNodeLevel(node.GUID);
            
            return node switch
            {
                GenericNodeData genericNode => genericNode.GenericCost,
                
                // Linear scaling: Level 1 = BaseCost, Level 2 = BaseCost * 2, etc.
                EmotionNodeData emotionNode => emotionNode.BaseEmotionCost + (currentLevel * emotionNode.BaseEmotionCost),
                
                _ => int.MaxValue // Failsafe: Prevent purchasing unknown node types
            };
        }

        public bool CanUnlock(BaseNodeData node)
        {
            int currentLevel = GetNodeLevel(node.GUID);

            // 1. Max Level bounds validation
            if (node is GenericNodeData && currentLevel >= 1) return false;
    
            // Explicitly check levels for Emotion nodes
            if (node is EmotionNodeData emotionNode && currentLevel >= emotionNode.MaxLevel) return false;

            // 2. Check Prerequisites (Prerequisites require at least level 1)
            foreach (string reqGuid in node.PrerequisiteGUIDs)
            {
                if (GetNodeLevel(reqGuid) == 0) return false;
            }

            // 3. Dynamic Cost Validation
            int requiredCost = GetNodeCost(node);
    
            // Safety: ensure cost is actually required
            if (requiredCost <= 0) return false;

            return node switch
            {
                GenericNodeData => genericPoints >= requiredCost,
                EmotionNodeData emNode => emotionPoints[emNode.RequiredEmotion] >= requiredCost,
                _ => false
            };
        }

        public bool TryUnlock(BaseNodeData node)
        {
            if (!CanUnlock(node)) return false;

            int costToDeduct = GetNodeCost(node);
            List<StatModifierData> statsToApply = new();

            if (node is GenericNodeData genericNode)
            {
                genericPoints -= costToDeduct;
                statsToApply = genericNode.GrantedStats;
            }
            else if (node is EmotionNodeData emotionNode)
            {
                emotionPoints[emotionNode.RequiredEmotion] -= costToDeduct;
                statsToApply = emotionNode.GrantedStats;
            }

            PlayerStats playerStats = FindAnyObjectByType<PlayerStats>();
            if (playerStats is not null && statsToApply != null)
            {
                foreach (StatModifierData mod in statsToApply)
                {
                    StatModifierData initializedMod = mod;
                    initializedMod.Source = node;
                    playerStats.GetStat(mod.Stat).AddModifier(initializedMod);
                }
            }

            if (!nodeLevels.ContainsKey(node.GUID)) nodeLevels[node.GUID] = 0;
            nodeLevels[node.GUID]++;

            Debug.Log($"[SkillTreeManager] Upgraded Node: {node.NodeName} to Level {nodeLevels[node.GUID]} (Cost: {costToDeduct})");
            OnSkillTreeUpdated?.Invoke();

            return true;
        }

        public bool IsUnlocked(BaseNodeData node)
        {
            return GetNodeLevel(node.GUID) > 0;
        }
    }
}