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
        public int channelTurns = 0;

        // Unity 6.4 supports SerializeReference, allowing polymorphic serialization in the Inspector!
        [SerializeReference] 
        public List<IAbilityCondition> conditions = new List<IAbilityCondition>();

        [SerializeReference] 
        public List<IAbilityEffect> effects = new List<IAbilityEffect>();

        public bool TryCast(AbilityContext context)
        {
            foreach (IAbilityCondition condition in conditions)
            {
                if (!condition.CanExecute(context)) return false;
            }

            foreach (IAbilityEffect effect in effects)
            {
                effect.Execute(context);
            }

            return true;
        }
    }
}