using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ability.NewAbilitySystem
{
    public interface ITurnEntity
    {
        int Initiative { get; }
        void PlanAction();
        void DrawIntents();
        void ExecuteAction();
        void EndTurn();
    }

    [DefaultExecutionOrder(-50)]
    public class TurnManager : MonoBehaviour
    {
        [Header("Turn Settings")] [Tooltip("Real-time seconds one turn takes to execute.")]
        public float secondsPerTurn = 1.0f;

        [Header("Time Scale Transitions")]
        [Tooltip("Percentage of the turn duration spent ramping up to 1x speed.")]
        [Range(0f, 0.5f)]
        // Capped at 0.5 (50%) so ramp up/down can never exceed 100% combined
        public float rampUpPercentage = 0.10f; // 10% default

        [Tooltip("Percentage of the turn duration spent ramping down to 0x speed.")] [Range(0f, 0.5f)]
        public float rampDownPercentage = 0.01f; // 1% default

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
            if (GameModeManager.Instance.CurrentMode == GameMode.Combat && !IsExecuting)
            {
                if (IntentDrawer.Instance != null) IntentDrawer.Instance.ClearAll();

                PrepareIterationBuffer();

                foreach (ITurnEntity entity in entityIterationBuffer) entity.PlanAction();
                foreach (ITurnEntity entity in entityIterationBuffer) entity.DrawIntents();

                if (!IsExecuting && pendingTurnCost > 0)
                {
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

        public void RequestTurns(int turnCost)
        {
            if (turnCost > pendingTurnCost) pendingTurnCost = turnCost;
        }

        private IEnumerator ExecuteTurnsRoutine()
        {
            IsExecuting = true;

            // 1. Prepare and Execute Intended Actions
            PrepareIterationBuffer();
            foreach (ITurnEntity entity in entityIterationBuffer)
                entity.ExecuteAction();

            // Calculate exact real-time duration of our 3 phases
            float rampUpDuration = secondsPerTurn * rampUpPercentage;
            float rampDownDuration = secondsPerTurn * rampDownPercentage;
            float holdDuration = secondsPerTurn - rampUpDuration - rampDownDuration;

            // --- PHASE 1: RAMP UP ---
            if (rampUpDuration > 0f)
            {
                float elapsed = 0f;
                // O(1) operation per frame. Binds strictly to unscaled time to avoid time dilation logic loops.
                while (elapsed < rampUpDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    SetTimeScale(Mathf.Lerp(0f, 1f, elapsed / rampUpDuration));
                    yield return null;
                }
            }

            // Snap to exactly 1 in case floating point math slightly over/undershot
            SetTimeScale(1f);

            // --- PHASE 2: HOLD ---
            if (holdDuration > 0f)
                // We MUST use real-time here because our timeScale is actively fluctuating during the routine
                yield return new WaitForSecondsRealtime(holdDuration);

            // --- PHASE 3: RAMP DOWN ---
            if (rampDownDuration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < rampDownDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    SetTimeScale(Mathf.Lerp(1f, 0f, elapsed / rampDownDuration));
                    yield return null;
                }
            }

            SetTimeScale(0f);
            CurrentTurn++;

            // --- ARCHITECTURAL FIX: MUTATE STATE BEFORE UPDATING UI ---
            PrepareIterationBuffer();
            foreach (ITurnEntity entity in entityIterationBuffer) entity.EndTurn();

            OnTurnTicked?.Invoke(CurrentTurn);

            IsExecuting = false;
            pendingTurnCost = 0;
        }

        private void HandleGameModeChanged(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Exploration:
                    if (turnExecutionCoroutine != null)
                    {
                        StopCoroutine(turnExecutionCoroutine);
                        IsExecuting = false;
                        pendingTurnCost = 0;
                    }

                    if (IntentDrawer.Instance != null) IntentDrawer.Instance.ClearAll();
                    SetTimeScale(1f); // Hard snap to 1 so the player immediately resumes control
                    break;

                case GameMode.Combat:
                    SetTimeScale(0f);
                    break;
            }
        }

        private void SetTimeScale(float targetTimeScale)
        {
            Time.timeScale = targetTimeScale;
            // Scaling FixedDeltaTime keeps Physics behavior (rigidbody velocity/collisions) perfectly smooth and deterministic
            Time.fixedDeltaTime = Mathf.Clamp(defaultFixedDeltaTime * targetTimeScale, 0.00001f, defaultFixedDeltaTime);
        }

        private void PrepareIterationBuffer()
        {
            entityIterationBuffer.Clear();
            entityIterationBuffer.AddRange(activeTurnEntities);
            entityIterationBuffer.Sort((a, b) => b.Initiative.CompareTo(a.Initiative));
        }
    }
}