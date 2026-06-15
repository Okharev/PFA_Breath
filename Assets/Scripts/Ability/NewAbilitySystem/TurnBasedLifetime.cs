using UnityEngine;

namespace Ability.NewAbilitySystem
{
    [RequireComponent(typeof(HazardVolume))]
    public class TurnBasedLifetime : MonoBehaviour
    {
        [Tooltip("How many full turns should this object exist before destroying itself?")]
        public int lifetimeTurns = 1;

        private int spawnTurn;

        private void Start()
        {
            // Record the turn we were spawned on
            spawnTurn = TurnManager.Instance.CurrentTurn;

            // Subscribe to the TurnManager event (Observer Pattern)
            TurnManager.OnTurnTicked += HandleTurnTicked;
        }

        private void OnDestroy()
        {
            // Always unsubscribe to prevent memory leaks!
            if (TurnManager.Instance != null) TurnManager.OnTurnTicked -= HandleTurnTicked;
        }

        private void HandleTurnTicked(int currentTurn)
        {
            if (currentTurn >= spawnTurn + lifetimeTurns) Destroy(gameObject);
        }
    }
}