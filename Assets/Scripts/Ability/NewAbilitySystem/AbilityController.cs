using System;
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
            if (TurnManager.Instance != null) TurnManager.Instance.UnregisterEntity(this);
        }

        public int Initiative => 1;

        public void PlanAction()
        {
        }


        public void DrawIntents()
        {
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
                return;
            }

            if (queuedAbility.TryCast(queuedContext))
            {
                ApplyCooldown(queuedAbility);
                OnAbilityExecuted?.Invoke();
            }

            queuedAbility = null;
        }

        public event Action OnAbilityExecuted;

        // --- NEW: CENTRALIZED COOLDOWN CHECKERS ---
// --- RELATIVE COOLDOWN TRACKER ---
        private readonly Dictionary<AbilityData, int> activeCooldowns = new();

        public bool IsOnCooldown(AbilityData ability)
        {
            return activeCooldowns.TryGetValue(ability, out int cd) && cd > 0;
        }

        private void ApplyCooldown(AbilityData ability)
        {
            if (ability.cooldownTurns > 0)
            {
                // +1 because EndTurn() will immediately decrement it by 1 at the end of this execution frame
                activeCooldowns[ability] = ability.cooldownTurns + 1;
            }
        }

        public int GetRemainingCooldown(AbilityData ability)
        {
            // Just return the raw integer. No more math against CurrentTurn!
            return activeCooldowns.TryGetValue(ability, out int cd) ? cd : 0;
        }

        // --- THE MAGIC DECREMENTER ---
        public void EndTurn()
        {
            // Safely iterate and tick down the cooldowns by 1 every round
            List<AbilityData> keys = new(activeCooldowns.Keys);
            foreach (AbilityData ability in keys)
            {
                if (activeCooldowns[ability] > 0)
                {
                    activeCooldowns[ability]--;
                }
            }
        }

        // --- REFACTORED: COMBAT QUEUE ROUTINE ---
        public bool QueueAbility(AbilityData ability, AbilityContext context)
        {
            // Enforce unified cooldown safety rule
            if (IsOnCooldown(ability))
                return false;

            queuedAbility = ability;
            queuedContext = context;
            currentChannelTurns = ability.channelTurns;

            return true;
        }

        // --- NEW: EXPLORATION IMMEDIATE ROUTINE ---
        public bool TryExecuteImmediate(AbilityData ability, AbilityContext context)
        {
            if (IsOnCooldown(ability))
                return false;

            if (ability.TryCast(context))
            {
                ApplyCooldown(ability);
                OnAbilityExecuted?.Invoke();
                return true;
            }

            return false;
        }
    }
}