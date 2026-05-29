using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ITurnEntity
{
    /// <summary>
    /// Called during the strategic pause. The entity decides its next move here.
    /// </summary>
    void PlanAction();

    /// <summary>
    /// Called on frame-1 of the turn execution. The entity commits the action.
    /// </summary>
    void ExecuteAction();

    /// <summary>
    /// Called when the turn finishes. Used for decrementing cooldowns or status effects.
    /// </summary>
    void EndTurn();
}

/// <summary>
///     Manages discrete turns in the game. Implements the Singleton and Observer patterns.
/// </summary>
[DefaultExecutionOrder(-50)]
public class TurnManager : MonoBehaviour
{
    private static readonly int GlobalUnscaledTime = Shader.PropertyToID("_GlobalUnscaledTime");

    [Header("Turn Settings")] [Tooltip("Real-time seconds one turn takes to execute.")]
    public float secondsPerTurn = 1.0f;

    private float defaultFixedDeltaTime;
    public static TurnManager Instance { get; private set; }
    public bool IsExecuting { get; private set; }
    public int CurrentTurn { get; private set; }

    
    // Observer Pattern: Broadcasts when a turn finishes
    public static event Action<int> OnTurnTicked;
    
    
    private readonly HashSet<ITurnEntity> activeTurnEntities = new HashSet<ITurnEntity>();

    public void RegisterEntity(ITurnEntity entity) => activeTurnEntities.Add(entity);
    public void UnregisterEntity(ITurnEntity entity) => activeTurnEntities.Remove(entity);

    
    private void Awake()
    {
        if (!Instance) Instance = this;
        else Destroy(gameObject);

        defaultFixedDeltaTime = Time.fixedDeltaTime;
    }

    private void OnEnable()
    {
        GameModeManager.OnGameModeChanged += HandleGameModeChanged;
    }

    private void OnDisable()
    {
        GameModeManager.OnGameModeChanged -= HandleGameModeChanged;
    }
    
    private void Update()
    {
        // Let entities dynamically plan their moves while time is paused
        if (GameModeManager.Instance.CurrentMode == GameMode.Combat && !IsExecuting)
        {
            foreach (var entity in activeTurnEntities)
            {
                entity.PlanAction();
            }
        }
    }

    // Your existing coroutine, upgraded to trigger the interface methods
    private IEnumerator ExecuteTurnsRoutine(int turnCost)
    {
        IsExecuting = true;
        SetTimeScale(1f); 

        for (int i = 0; i < turnCost; i++)
        {
            // 1. EXECUTE phase: Tell all entities to fire their queued actions
            foreach (var entity in activeTurnEntities)
            {
                entity.ExecuteAction();
            }

            // 2. WAIT for the real-time turn duration
            float elapsed = 0f;
            while (elapsed < secondsPerTurn)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            CurrentTurn++;
            
            // 3. CLEANUP phase: Process cooldowns
            foreach (var entity in activeTurnEntities)
            {
                entity.EndTurn();
            }
        }

        if (GameModeManager.Instance.CurrentMode == GameMode.Combat)
        {
            SetTimeScale(0f); 
        }
    
        IsExecuting = false;
    }
    
    private void HandleGameModeChanged(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Exploration:
                SetTimeScale(1f); // Free movement, real-time
                break;
            case GameMode.Combat:
                SetTimeScale(0f); // Pause time, await turn commands
                break;
        }
    }


    /// <summary>
    ///     Queues the execution of a set number of turns.
    /// </summary>
    public void ExecuteTurns(int turnCost)
    {
        if (!IsExecuting && turnCost > 0) StartCoroutine(ExecuteTurnsRoutine(turnCost));
    }


    private void SetTimeScale(float targetTimeScale)
    {
        Time.timeScale = targetTimeScale;
        Time.fixedDeltaTime = Mathf.Clamp(defaultFixedDeltaTime * targetTimeScale, 0.00001f, defaultFixedDeltaTime);
    }
}