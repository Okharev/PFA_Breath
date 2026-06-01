using System.Collections.Generic;
using UnityEngine;

namespace Ability.NewAbilitySystem
{
    [CreateAssetMenu(menuName = "Strategy/Ability Data")]
    public class AbilityData : ScriptableObject
    {
        public string abilityName;
        public int turnCost = 1;
        public int cooldownTurns = 3;
        public int channelTurns;

        // Apply both attributes to unlock the dropdown in the inspector
        [SerializeReference] [SubclassSelector]
        public List<IAbilityCondition> conditions = new();

        [SerializeReference] [SubclassSelector]
        public List<IAbilityEffect> effects = new();

        public bool TryCast(AbilityContext context)
        {
            // Evaluate Conditions safely
            foreach (IAbilityCondition condition in conditions)
                if (condition != null && !condition.CanExecute(context))
                    return false;

            // Execute Effects safely
            foreach (IAbilityEffect effect in effects) effect?.Execute(context);

            return true;
        }
    }
}