using System;
using System.Collections.Generic;
using UnityEngine;

namespace Skills
{
    // --- ENUMS ---
    public enum StatType
    {
        Damage,
        ProjectileCount,
        OxygenCostReduction,
        Spread,
        MovementSpeed
    }

    public enum ModifierType
    {
        Flat = 100,
        AdditivePercent = 200,
        MultiplicativePercent = 300
    }

    public enum EmotionType
    {
        Red,
        Green,
        Blue,
        Yellow,
        White
    }

    public enum AbilitySlot
    {
        Primary,
        Secondary,
        Movement,
        Special,
        Dash
    }

    public enum NodeType
    {
        Generic,
        Emotion
    }

    // --- STAT SYSTEM ---
    [Serializable]
    public struct StatModifierData
    {
        public StatType Stat;
        public ModifierType Type;
        public float Value;
        public object Source; // Allows us to remove modifiers tied to a specific skill node if the player "respecs"
    }

    [Serializable]
    public class Stat
    {
        public float BaseValue;

        private readonly List<StatModifierData> modifiers = new();
        private bool isDirty = true; // DIRTY FLAG: Tells the system if math needs to be recalculated.
        private float lastCalculatedValue;

        public Stat(float baseValue)
        {
            BaseValue = baseValue;
        }

        // O(1) Time Complexity on read. 
        // We ONLY do the heavy math if 'isDirty' is true (which only happens when unlocking a skill).
        public float Value
        {
            get
            {
                if (isDirty)
                {
                    lastCalculatedValue = CalculateFinalValue();
                    isDirty = false;
                }

                return lastCalculatedValue;
            }
        }

        public void AddModifier(StatModifierData modifier)
        {
            modifiers.Add(modifier);
            modifiers.Sort((a, b) => a.Type.CompareTo(b.Type)); // Enforces Order of Operations
            isDirty = true;
        }

        private float CalculateFinalValue()
        {
            float finalValue = BaseValue;
            float sumPercentAdd = 0;

            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifierData mod = modifiers[i];

                switch (mod.Type)
                {
                    case ModifierType.Flat:
                        // 1. Flat additions happen first (e.g., Base 10 + 5 = 15)
                        finalValue += mod.Value;
                        break;
                    case ModifierType.AdditivePercent:
                    {
                        // 2. Accumulate all additive percentages (e.g., +10% and +15% becomes +25% or 0.25)
                        sumPercentAdd += mod.Value;
            
                        // If we are at the end of the list, OR the next modifier is a different type (Multiplicative),
                        // it means we have finished gathering all Additive modifiers. Apply them now.
                        if (i + 1 >= modifiers.Count || modifiers[i + 1].Type != ModifierType.AdditivePercent)
                        {
                            finalValue *= (1 + sumPercentAdd);
                            sumPercentAdd = 0; // Reset for safety
                        }

                        break;
                    }
                    case ModifierType.MultiplicativePercent:
                        // 3. Multiplicative modifiers apply last, compounding on top of everything else (e.g., x1.1)
                        finalValue *= mod.Value;
                        break;
                }
            }

            // Round to 4 decimal places to prevent nasty float precision errors (e.g., ending up with 10.0000001)
            return (float)Math.Round(finalValue, 4); 
        }
    }

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

    [Serializable]
    public struct SkillCost
    {
        public int GenericPoints;
        public int EmotionPoints;
        public EmotionType RequiredEmotion;
    }

    [Serializable]
    public class SkillNodeData
    {
        [HideInInspector] public string GUID = Guid.NewGuid().ToString();
        [HideInInspector] public Rect Position;

        [Header("Identity")] public string NodeName;

        [TextArea] public string Description;

        // NEW: Add the type to the data
        public NodeType NodeType = NodeType.Generic; // Default to Generic

        [Header("Economics")] public SkillCost Cost;

        [Header("Passive Upgrades")] public List<StatModifierData> GrantedStats = new();

        [Header("Active Abilities")] public bool UnlocksAbility;

        public string GrantedAbilityId;
        public AbilitySlot IntendedSlot;

        [HideInInspector] public List<string> PrerequisiteGUIDs = new();
    }
}