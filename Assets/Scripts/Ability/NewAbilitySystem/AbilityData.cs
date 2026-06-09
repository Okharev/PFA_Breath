using System.Collections.Generic;
using UnityEngine;

namespace Ability.NewAbilitySystem
{
    [CreateAssetMenu(menuName = "Strategy/Ability Data")]
    public class AbilityData : ScriptableObject
    {
        public Sprite Icon;
        
        public string abilityName;
        public int turnCost = 1;
        public int cooldownTurns = 3;
        public int channelTurns;

        [Header("Micro-Timing")]
        [Tooltip("Percentage of the turn duration to wait before firing. 0 = Instant, 0.5 = Halfway, 1.0 = End of turn.")]
        [Range(0f, 1f)] 
        public float executionDelayFraction = 0f;

        [SerializeReference, SubclassSelector]
        public List<IAbilityCondition> conditions = new();

        [SerializeReference, SubclassSelector]
        public List<IAbilityEffect> effects = new();

        public bool TryCast(AbilityContext context)
        {
            foreach (IAbilityCondition condition in conditions)
                if (condition != null && !condition.CanExecute(context))
                    return false;

            foreach (IAbilityEffect effect in effects) effect?.Execute(context);

            return true;
        }
    }
}