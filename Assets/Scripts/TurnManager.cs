using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Manages discrete turns in the game. Implements the Singleton and Observer patterns.
/// </summary>
[DefaultExecutionOrder(-50)]
public class TurnManager : MonoBehaviour
{
    private static readonly int GlobalUnscaledTime = Shader.PropertyToID("_GlobalUnscaledTime");
    public static TurnManager Instance { get; private set; }

    [Header("Turn Settings")]
    [Tooltip("Real-time seconds one turn takes to execute.")]
    public float secondsPerTurn = 1.0f;

    // Observer Pattern: Broadcasts when a turn finishes
    public static event Action<int> OnTurnTicked;

    private float defaultFixedDeltaTime;
    public bool IsExecuting { get; private set; }
    public int CurrentTurn { get; private set; }

    private void Update()
    {
        Shader.SetGlobalFloat(GlobalUnscaledTime, Time.unscaledTime);
    }

    private void Awake()
    {
        if (!Instance) Instance = this;
        else Destroy(gameObject);

        defaultFixedDeltaTime = Time.fixedDeltaTime;
        SetTimeScale(0f); // Start paused
    }

    /// <summary>
    /// Queues the execution of a set number of turns.
    /// </summary>
    public void ExecuteTurns(int turnCost)
    {
        if (!IsExecuting && turnCost > 0)
        {
            StartCoroutine(ExecuteTurnsRoutine(turnCost));
        }
    }

    private IEnumerator ExecuteTurnsRoutine(int turnCost)
    {
        IsExecuting = true;
        SetTimeScale(1f); // Resume time

        for (int i = 0; i < turnCost; i++)
        {
            float elapsed = 0f;
            
            // Wait for exactly 'secondsPerTurn'
            while (elapsed < secondsPerTurn)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            CurrentTurn++;
            OnTurnTicked?.Invoke(CurrentTurn); // Notify all listeners that a turn completed
        }

        SetTimeScale(0f); // Pause time
        IsExecuting = false;
    }

    private void SetTimeScale(float targetTimeScale)
    {
        Time.timeScale = targetTimeScale;
        Time.fixedDeltaTime = Mathf.Clamp(defaultFixedDeltaTime * targetTimeScale, 0.00001f, defaultFixedDeltaTime);
    }
}