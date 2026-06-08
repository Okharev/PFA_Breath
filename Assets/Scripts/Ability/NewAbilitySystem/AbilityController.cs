using System;
using System.Collections;
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

            // --- REFACTORED: Unified Delay Logic ---
            if (queuedAbility.executionDelayFraction > 0f)
            {
                StartCoroutine(DelayedExecutionRoutine(queuedAbility, queuedContext));
            }
            else
            {
                ExecuteInstantly(queuedAbility, queuedContext);
            }

            queuedAbility = null;
        }
        
        private IEnumerator DelayedExecutionRoutine(AbilityData ability, AbilityContext context)
        {
            // O(1) mathematical lookup synced perfectly to your TurnManager
            float delayInRealSeconds = TurnManager.Instance.secondsPerTurn * ability.executionDelayFraction;
            
            // We use Realtime because TurnManager manipulates Time.timeScale during Phase 1 & 3
            yield return new WaitForSecondsRealtime(delayInRealSeconds);

            ExecuteInstantly(ability, context);
        }

        private void ExecuteInstantly(AbilityData ability, AbilityContext context)
        {
            if (ability.TryCast(context))
            {
                ApplyCooldown(ability);
                OnAbilityExecuted?.Invoke();
            }
        }

        public event Action OnAbilityExecuted;
        
        public AbilityData LockedComboAbility { get; private set; }
        
        public void LockCombo(AbilityData ability)
        {
            LockedComboAbility = ability;
        }

        public void UnlockCombo()
        {
            LockedComboAbility = null;
        }

        // --- RELATIVE COOLDOWN TRACKER ---
        private readonly Dictionary<AbilityData, int> activeCooldowns = new();

        // --- NEW: Allows combo effects to refund premature cooldowns ---
        public void RefundCooldown(AbilityData ability)
        {
            if (ability != null && activeCooldowns.ContainsKey(ability))
            {
                activeCooldowns[ability] = 0;
            }
        }
        
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
            return activeCooldowns.GetValueOrDefault(ability, 0);
        }

        private readonly Dictionary<string, int> transientStates = new();

        public int GetTransientState(string key) => transientStates.GetValueOrDefault(key, 0);
        public void SetTransientState(string key, int value) => transientStates[key] = value;


        public bool QueueAbility(AbilityData ability, AbilityContext context)
        {
            // NEW: Reject if we are locked into a different combo!
            if (LockedComboAbility != null && ability != LockedComboAbility)
            {
                Debug.Log($"[Action Blocked] You must finish the {LockedComboAbility.abilityName} combo first!");
                return false;
            }

            if (IsOnCooldown(ability)) return false;

            queuedAbility = ability;
            queuedContext = context;
            currentChannelTurns = ability.channelTurns;
            return true;
        }

        public bool TryExecuteImmediate(AbilityData ability, AbilityContext context)
        {
            // NEW: Reject if we are locked into a different combo!
            if (LockedComboAbility != null && ability != LockedComboAbility)
            {
                Debug.Log($"[Action Blocked] You must finish the {LockedComboAbility.abilityName} combo first!");
                return false;
            }

            if (IsOnCooldown(ability)) return false;

            if (ability.TryCast(context))
            {
                ApplyCooldown(ability);
                OnAbilityExecuted?.Invoke();
                return true;
            }
            return false;
        }
        
        public void EndTurn()
        {
            List<AbilityData> keys = new(activeCooldowns.Keys);
            foreach (AbilityData ability in keys)
            {
                if (activeCooldowns[ability] > 0) activeCooldowns[ability]--;
            }
            
            transientStates.Clear(); 
            
            // NEW: Safety wipe! If the turn is forced to end (e.g., combat ends or player is stunned),
            // we must clear the lock so the player isn't soft-locked next turn.
            UnlockCombo(); 
        }
    }
}