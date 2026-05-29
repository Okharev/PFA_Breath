using System.Collections.Generic;
using UnityEngine;

namespace Ability.NewAbilitySystem
{
    [RequireComponent(typeof(HealthComponent))]
    public class AbilityController : MonoBehaviour, ITurnEntity
    {
        // Dictionary provides O(1) time complexity for cooldown lookups
        private readonly Dictionary<AbilityData, int> activeCooldowns = new Dictionary<AbilityData, int>();
    
        private AbilityData queuedAbility;
        private AbilityContext queuedContext;
        private int currentChannelTurns;

        private void Start()
        {
            TurnManager.Instance.RegisterEntity(this);
        }

        private void OnDestroy()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.UnregisterEntity(this);
        }

        public void QueueAbility(AbilityData ability, AbilityContext context)
        {
            if (activeCooldowns.ContainsKey(ability)) return; // On Cooldown

            queuedAbility = ability;
            queuedContext = context;
            currentChannelTurns = ability.channelTurns;
        
            // Tell the TurnManager to advance time based on this ability's cost
            TurnManager.Instance.ExecuteTurns(ability.turnCost);
        }

        public void PlanAction()
        {
            // AI logic goes here to populate 'queuedAbility'
        }

        public void ExecuteAction()
        {
            if (queuedAbility == null) return;

            if (currentChannelTurns > 0)
            {
                // Still channeling
                currentChannelTurns--;
                return;
            }

            // Execution phase
            if (queuedAbility.TryCast(queuedContext))
            {
                if (queuedAbility.cooldownTurns > 0)
                {
                    activeCooldowns[queuedAbility] = queuedAbility.cooldownTurns;
                }
            }
        
            queuedAbility = null; 
        }

        public void EndTurn()
        {
            // Decrement cooldowns at the end of the turn cycle
            List<AbilityData> keys = new List<AbilityData>(activeCooldowns.Keys);
            foreach (var key in keys)
            {
                activeCooldowns[key]--;
                if (activeCooldowns[key] <= 0)
                {
                    activeCooldowns.Remove(key);
                }
            }
        }
    }
}