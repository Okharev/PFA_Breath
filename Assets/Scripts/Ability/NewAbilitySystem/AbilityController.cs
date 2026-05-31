using System.Collections.Generic;
using UnityEngine;

namespace Ability.NewAbilitySystem
{
    public class AbilityController : MonoBehaviour, ITurnEntity
    {
        private readonly Dictionary<AbilityData, int> abilityAvailableAtTurn = new();
        private int currentChannelTurns;

        private AbilityData queuedAbility;
        private AbilityContext queuedContext;

        public bool HasQueuedAbility => queuedAbility != null;

        private void Start()
        {
            TurnManager.Instance.RegisterEntity(this);
        }

        private void OnDestroy()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.UnregisterEntity(this);
        }

        public int Initiative => 1;

        public void PlanAction()
        {
            // AI logic goes here to populate 'queuedAbility'
        }

        public void DrawIntents()
        {
            // If we have an ability queued, ask its effects to draw themselves!
            if (queuedAbility != null)
                foreach (IAbilityEffect effect in queuedAbility.effects)
                    if (effect is IPreviewableEffect preview)
                        preview.DrawPreview(queuedContext, IntentDrawer.Instance);
        }

        public void ExecuteAction()
        {
            if (queuedAbility == null) return;

            if (currentChannelTurns > 0)
            {
                currentChannelTurns--;
                return; // Still channeling, action will resolve on a later turn
            }

            // Execute logic
            if (queuedAbility.TryCast(queuedContext))
                if (queuedAbility.cooldownTurns > 0)
                {
                    // Store the absolute turn number when it's ready again
                    int readyTurn = TurnManager.Instance.CurrentTurn + queuedAbility.cooldownTurns;
                    abilityAvailableAtTurn[queuedAbility] = readyTurn;
                }

            queuedAbility = null;
        }

        public void EndTurn()
        {
        }


// 1. Change void to bool
        public bool QueueAbility(AbilityData ability, AbilityContext context)
        {
            if (abilityAvailableAtTurn.TryGetValue(ability, out int availableTurn))
                if (TurnManager.Instance.CurrentTurn < availableTurn)
                    return false; // On Cooldown, failed to queue

            queuedAbility = ability;
            queuedContext = context;
            currentChannelTurns = ability.channelTurns;

            // REMOVED: TurnManager.Instance.RequestTurns(1); 
            // The controller should just hold the data. It shouldn't dictate time!

            return true; // Successfully queued
        }
    }
}