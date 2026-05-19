using System.Collections;
using UnityEngine;

public class TimeTickManager : MonoBehaviour
{
    [Header("Time Settings")] 
    public float transitionDuration = 0.2f;

    private bool isTimeFlowing;
    public static TimeTickManager Instance { get; private set; }

    // Debug Variables
    public string CurrentPhase { get; private set; } = "Paused";
    public float CurrentTimeScale => Time.timeScale;
    public float ActionProgress { get; private set; } 
    public float TotalActionDuration { get; private set; }

    // Cache the default physics step (usually 0.02f)
    private float defaultFixedDeltaTime;

    private void Awake()
    {
        if (Instance is null) Instance = this;
        else Destroy(gameObject);

        defaultFixedDeltaTime = Time.fixedDeltaTime;
        SetTime(0f); // Initialize correctly
    }

    public void TriggerActionTick(float actionDuration)
    {
        if (!isTimeFlowing) StartCoroutine(SmoothTimeFlowRoutine(actionDuration));
    }

    private IEnumerator SmoothTimeFlowRoutine(float actionDuration)
    {
        isTimeFlowing = true;
        TotalActionDuration = actionDuration;

        float actualTransitionTime = Mathf.Min(transitionDuration, actionDuration / 2f);
        float holdDuration = actionDuration - actualTransitionTime * 2f;
        float elapsed = 0f;

        // PHASE 1: RAMP UP
        CurrentPhase = "Ramping Up";
        while (elapsed < actualTransitionTime)
        {
            elapsed += Time.unscaledDeltaTime;
            // Use our custom method to apply both time and physics scales
            SetTime(Mathf.Lerp(0f, 1f, elapsed / actualTransitionTime));
            yield return null;
        }

        SetTime(1f);

        // PHASE 2: ACTION EXECUTION
        CurrentPhase = "Action Executing";
        elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.deltaTime;
            ActionProgress = elapsed / holdDuration;
            yield return null;
        }

        // PHASE 3: RAMP DOWN
        CurrentPhase = "Ramping Down";
        elapsed = 0f;
        while (elapsed < actualTransitionTime)
        {
            elapsed += Time.unscaledDeltaTime;
            SetTime(Mathf.Lerp(1f, 0f, elapsed / actualTransitionTime));
            yield return null;
        }

        // FINISH
        SetTime(0f);
        ActionProgress = 0f;
        CurrentPhase = "Paused";
        isTimeFlowing = false;
    }

    /// <summary>
    /// Safely synchronizes the physics engine's tick rate with the visual time scale.
    /// </summary>
    private void SetTime(float targetTimeScale)
    {
        Time.timeScale = targetTimeScale;
        
        // Clamp at a very low value to prevent divide-by-zero physics errors when paused
        Time.fixedDeltaTime = Mathf.Clamp(defaultFixedDeltaTime * targetTimeScale, 0.00001f, defaultFixedDeltaTime);
    }

    public bool IsTimeFlowing()
    {
        return isTimeFlowing;
    }
}