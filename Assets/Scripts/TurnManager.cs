using System;
using System.Collections;
using UnityEngine;

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
    
    private void HandleGameModeChanged(GameMode mode)
    {
        if (mode == GameMode.Exploration)
        {
            SetTimeScale(1f); // Free movement, real-time
        }
        else if (mode == GameMode.Combat)
        {
            SetTimeScale(0f); // Pause time, await turn commands
        }
    }
    
    private void Update()
    {
        Shader.SetGlobalFloat(GlobalUnscaledTime, Time.unscaledTime);
    }

    // Observer Pattern: Broadcasts when a turn finishes
    public static event Action<int> OnTurnTicked;

    /// <summary>
    ///     Queues the execution of a set number of turns.
    /// </summary>
    public void ExecuteTurns(int turnCost)
    {
        if (!IsExecuting && turnCost > 0) StartCoroutine(ExecuteTurnsRoutine(turnCost));
    }

    private IEnumerator ExecuteTurnsRoutine(int turnCost)
    {
        IsExecuting = true;
    
        // Always ensure time is flowing while processing a turn
        SetTimeScale(1f); 

        for (int i = 0; i < turnCost; i++)
        {
            float elapsed = 0f;

            while (elapsed < secondsPerTurn)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            CurrentTurn++;
            OnTurnTicked?.Invoke(CurrentTurn); // Notify listeners
        }

        // --- THE FIX ---
        // Only pause time if we are actively in strategic combat.
        // Otherwise, let time flow normally for exploration!
        if (GameModeManager.Instance.CurrentMode == GameMode.Combat)
        {
            SetTimeScale(0f); 
        }
    
        IsExecuting = false;
    }

    private void SetTimeScale(float targetTimeScale)
    {
        Time.timeScale = targetTimeScale;
        Time.fixedDeltaTime = Mathf.Clamp(defaultFixedDeltaTime * targetTimeScale, 0.00001f, defaultFixedDeltaTime);
    }
}