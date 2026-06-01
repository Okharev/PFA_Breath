using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ability.NewAbilitySystem
{
    public interface ITurnEntity
    {
        int Initiative { get; }

        /// <summary>
        ///     Called during the strategic pause. The entity decides its next move here (e.g., AI pathfinding, queueing
        ///     abilities).
        /// </summary>
        void PlanAction();

        /// <summary>
        ///     NEW: Called immediately after PlanAction during the pause. Entities submit their visual intents here.
        /// </summary>
        void DrawIntents();

        /// <summary>
        ///     Called on frame-1 of the turn execution. The entity commits the queued action.
        /// </summary>
        void ExecuteAction();

        /// <summary>
        ///     Called when the turn finishes. Used for decrementing cooldowns, ticking poison damage, or resolving status effects.
        /// </summary>
        void EndTurn();
    }

    [DefaultExecutionOrder(-50)]
    public class TurnManager : MonoBehaviour
    {
        [Header("Turn Settings")] [Tooltip("Real-time seconds one turn takes to execute.")]
        public float secondsPerTurn = 1.0f;

        // O(1) Add/Remove, but we need safe iteration.
        private readonly HashSet<ITurnEntity> activeTurnEntities = new();

        // Cached list to avoid allocating memory during iteration (fixes InvalidOperationException)
        private readonly List<ITurnEntity> entityIterationBuffer = new();

        private float defaultFixedDeltaTime;

        private int pendingTurnCost;
        private Coroutine turnExecutionCoroutine;
        public static TurnManager Instance { get; private set; }

        public bool IsExecuting { get; private set; }
        public int CurrentTurn { get; private set; }

        private void Awake()
        {
            if (!Instance) Instance = this;
            else Destroy(gameObject);

            defaultFixedDeltaTime = Time.fixedDeltaTime;
        }

        private void Update()
        {
            // Plan moves if we are in Combat and not currently executing a turn
            if (GameModeManager.Instance.CurrentMode == GameMode.Combat && !IsExecuting)
            {
                // NEW: Clear old visuals at the start of the frame before entities rethink their plans
                if (IntentDrawer.Instance != null) IntentDrawer.Instance.ClearAll();

                // Copy to buffer to prevent modification exceptions if entities spawn/die during planning
                PrepareIterationBuffer();

                foreach (ITurnEntity entity in entityIterationBuffer) entity.PlanAction();

                // NEW: Allow all entities to draw their confirmed intent for this frame
                foreach (ITurnEntity entity in entityIterationBuffer) entity.DrawIntents();

                // --- Safe Execution Phase ---
                // After ALL entities have finished planning without interruption,
                // we safely check if a turn needs to be executed.
                if (!IsExecuting && pendingTurnCost > 0)
                {
                    // NEW: Erase all intent visuals the exact moment the turn begins executing
                    if (IntentDrawer.Instance != null) IntentDrawer.Instance.ClearAll();

                    turnExecutionCoroutine = StartCoroutine(ExecuteTurnsRoutine());
                }
            }
        }

        private void OnEnable()
        {
            GameModeManager.OnGameModeChanged += HandleGameModeChanged;
        }

        private void OnDisable()
        {
            GameModeManager.OnGameModeChanged -= HandleGameModeChanged;
        }

        public static event Action<int> OnTurnTicked;

        public void RegisterEntity(ITurnEntity entity)
        {
            activeTurnEntities.Add(entity);
        }

        public void UnregisterEntity(ITurnEntity entity)
        {
            activeTurnEntities.Remove(entity);
        }

        /// <summary>
        ///     Request turns to be executed. The manager will execute the highest requested amount.
        /// </summary>
        public void RequestTurns(int turnCost)
        {
            if (turnCost > pendingTurnCost) pendingTurnCost = turnCost;
        }

        private IEnumerator ExecuteTurnsRoutine()
        {
            IsExecuting = true;
            SetTimeScale(1f);

            // Execute exactly ONE turn's worth of time, regardless of how many turns an ability costs.
            PrepareIterationBuffer();

            foreach (ITurnEntity entity in entityIterationBuffer)
                entity.ExecuteAction(); // Entities handle their own channeling state internally

            // Wait for the duration of the turn (using scaled time so pauses/slow-mo affect it)
// Wait for the duration of the turn
            yield return new WaitForSeconds(secondsPerTurn);

            CurrentTurn++;

            // --- ARCHITECTURAL FIX: MUTATE STATE BEFORE UPDATING UI ---
        
            // 1. Resolve all backend math (like cooldown decrements) first!
            PrepareIterationBuffer();
            foreach (ITurnEntity entity in entityIterationBuffer) entity.EndTurn();
        
            // 2. NOW we tell the UI to draw the finished state
            OnTurnTicked?.Invoke(CurrentTurn);

            // ----------------------------------------------------------

            SetTimeScale(0f);
            IsExecuting = false;
            pendingTurnCost = 0;
        }

        /// <summary>
        ///     Handles the State Pattern transition between game modes gracefully.
        /// </summary>
        private void HandleGameModeChanged(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Exploration:
                    // Force break out of turn execution if we switch modes mid-combat
                    if (turnExecutionCoroutine != null)
                    {
                        StopCoroutine(turnExecutionCoroutine);
                        IsExecuting = false;
                        pendingTurnCost = 0;
                    }

                    // NEW: Ensure no combat graphics linger on the screen during exploration
                    if (IntentDrawer.Instance != null) IntentDrawer.Instance.ClearAll();

                    SetTimeScale(1f);
                    break;

                case GameMode.Combat:
                    SetTimeScale(0f);
                    break;
            }
        }

        private void SetTimeScale(float targetTimeScale)
        {
            Time.timeScale = targetTimeScale;
            Time.fixedDeltaTime = Mathf.Clamp(defaultFixedDeltaTime * targetTimeScale, 0.00001f, defaultFixedDeltaTime);
        }

        private void PrepareIterationBuffer()
        {
            entityIterationBuffer.Clear();
            entityIterationBuffer.AddRange(activeTurnEntities);
            // Sort descending by Initiative. Highest initiative acts first.
            entityIterationBuffer.Sort((a, b) => b.Initiative.CompareTo(a.Initiative));
        }
    }
}