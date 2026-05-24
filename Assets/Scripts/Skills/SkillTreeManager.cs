using System;
using System.Collections.Generic;
using Ability;
using Skills.Skills;
using UnityEngine;

namespace Skills
{
    public class SkillTreeManager : MonoBehaviour
    {
        [Header("Player Currencies")] public int genericPoints;

        public readonly Dictionary<EmotionType, int> emotionPoints = new();


        private readonly Dictionary<AbilitySlot, string> equippedNodes = new();

        // ARCHITECTURAL UPGRADE: O(1) level map replacing the old binary HashSet
        private readonly Dictionary<string, int> nodeLevels = new();

        public static SkillTreeManager Instance { get; private set; }

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            foreach (EmotionType emotion in Enum.GetValues(typeof(EmotionType))) emotionPoints[emotion] = 0;

            // Default sandbox points
            AddEmotionPoints(EmotionType.Red, 5);
            AddGenericPoints(8);
        }

        public static event Action OnSkillTreeUpdated;

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
            // Ensure the node is actually unlocked and provides an ability
            if (GetNodeLevel(node.GUID) <= 0 || !node.UnlocksAbility) return;

            // 1. Update the UI Data State (this inherently un-equips whatever was in this slot before)
            equippedNodes[node.IntendedSlot] = node.GUID;

            // 2. Push the change to the gameplay layer
            PlayerAbilityController playerController = FindAnyObjectByType<PlayerAbilityController>();
            if (playerController != null)
                playerController.EquipAbility(node.GrantedAbilityId, node.IntendedSlot, GetNodeLevel(node.GUID));

            Debug.Log($"[SkillTreeManager] Equipped {node.NodeName} to {node.IntendedSlot} slot.");

            // 3. Notify the Canvas to repaint borders
            OnSkillTreeUpdated?.Invoke();
        }

        // UPGRADED: Now handles Unequipping if the node is already active
        public void ToggleEquipNode(EmotionNodeData node)
        {
            if (GetNodeLevel(node.GUID) <= 0 || !node.UnlocksAbility) return;

            bool isCurrentlyEquipped = IsNodeEquipped(node);
            PlayerAbilityController playerController = FindAnyObjectByType<PlayerAbilityController>();

            if (isCurrentlyEquipped)
            {
                // UNEQUIP LOGIC
                equippedNodes.Remove(node.IntendedSlot);
                if (playerController != null) playerController.EquipDefaultAbility(node.IntendedSlot);
                Debug.Log($"[SkillTreeManager] Unequipped {node.NodeName}. Reverted to default.");
            }
            else
            {
                // EQUIP LOGIC (Overwrites whatever was currently in this slot)
                equippedNodes[node.IntendedSlot] = node.GUID;
                if (playerController != null)
                    playerController.EquipAbility(node.GrantedAbilityId, node.IntendedSlot, GetNodeLevel(node.GUID));
                Debug.Log($"[SkillTreeManager] Equipped {node.NodeName} to {node.IntendedSlot} slot.");
            }

            // Repaint the UI borders instantly
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

        public bool CanUnlock(BaseNodeData node)
        {
            int currentLevel = GetNodeLevel(node.GUID);

            // 1. Max Level bounds validation
            if (node is GenericNodeData && currentLevel >= 1) return false; // Generic nodes are single purchase
            // FIX: Renamed 'emotionNode' to 'eNode' here to clear the method scope collision
            if (node is EmotionNodeData eNode && currentLevel >= eNode.MaxLevel) return false;

            // 2. Check Prerequisites (Prerequisites require at least level 1)
            foreach (string reqGuid in node.PrerequisiteGUIDs)
                if (GetNodeLevel(reqGuid) == 0)
                    return false;

            // 3. Check Costs
            return node switch
            {
                GenericNodeData genericNode => genericPoints >= genericNode.GenericCost,
                EmotionNodeData emotionNode => emotionPoints[emotionNode.RequiredEmotion] >=
                                               emotionNode.BaseEmotionCost,
                _ => true
            };
        }

        public bool TryUnlock(BaseNodeData node)
        {
            if (!CanUnlock(node)) return false;

            List<StatModifierData> statsToApply = new();

            if (node is GenericNodeData genericNode)
            {
                genericPoints -= genericNode.GenericCost;
                statsToApply = genericNode.GrantedStats;
            }
            else if (node is EmotionNodeData emotionNode)
            {
                emotionPoints[emotionNode.RequiredEmotion] -= emotionNode.BaseEmotionCost;
                statsToApply = emotionNode.GrantedStats;
            }

            PlayerStats playerStats = FindAnyObjectByType<PlayerStats>();
            if (playerStats is not null && statsToApply != null)
                foreach (StatModifierData mod in statsToApply)
                {
                    StatModifierData initializedMod = mod;
                    initializedMod.Source = node;
                    playerStats.GetStat(mod.Stat).AddModifier(initializedMod);
                }

            // Increment level state safely inside our dictionary map
            if (!nodeLevels.ContainsKey(node.GUID)) nodeLevels[node.GUID] = 0;
            nodeLevels[node.GUID]++;

            Debug.Log($"[SkillTreeManager] Upgraded Node: {node.NodeName} to Level {nodeLevels[node.GUID]}");
            OnSkillTreeUpdated?.Invoke();

            return true;
        }

        public bool IsUnlocked(BaseNodeData node)
        {
            return GetNodeLevel(node.GUID) > 0;
        }
    }
}