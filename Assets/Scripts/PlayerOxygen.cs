using System;
using UnityEngine;

/// <summary>
/// Manages the player's integer-based oxygen resource.
/// Broadcasts granular state changes via events to keep external systems decoupled.
/// </summary>
public class PlayerOxygen : MonoBehaviour
{
    [Header("Resource Settings")]
    [Tooltip("The maximum amount of oxygen the player can hold.")]
    [SerializeField] private int maxOxygen = 100;
    
    [SerializeField] private int currentOxygen;

    // Granular Observer Events
    public event Action<int, int> OnOxygenChanged; // Passes (currentOxygen, maxOxygen)
    public event Action<int> OnOxygenLost;         // Passes (amount lost)
    public event Action<int> OnOxygenGained;       // Passes (amount gained)
    public event Action OnOxygenDepleted;          // Triggers when oxygen reaches 0

    private void Awake()
    {
        // Initialize state in Awake so it's ready before other scripts subscribe in Start
        currentOxygen = maxOxygen;
    }

    private void Start()
    {
        // Broadcast initial state
        OnOxygenChanged?.Invoke(currentOxygen, maxOxygen);
    }

    /// <summary>
    /// Checks if the requested amount of oxygen is available. O(1) complexity.
    /// </summary>
    public bool CanAfford(int amount)
    {
        return currentOxygen >= amount;
    }

    /// <summary>
    /// Attempts to consume oxygen. Returns true if successful, false if not enough oxygen.
    /// </summary>
    public bool TryConsume(int amount)
    {
        if (!CanAfford(amount)) return false;

        currentOxygen -= amount;
        
        // Fire the specific loss event, then the general change event
        OnOxygenLost?.Invoke(amount);
        OnOxygenChanged?.Invoke(currentOxygen, maxOxygen);

        if (currentOxygen <= 0)
        {
            OnOxygenDepleted?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// Adds oxygen, clamped to the maximum. 
    /// </summary>
    public void Replenish(int amount)
    {
        if (currentOxygen >= maxOxygen) return; // Prevent firing events if already full

        // Calculate the actual amount gained (prevents over-reporting if close to max)
        int actualGain = Mathf.Min(amount, maxOxygen - currentOxygen);
        currentOxygen += actualGain;

        // Fire the specific gain event, then the general change event
        OnOxygenGained?.Invoke(actualGain);
        OnOxygenChanged?.Invoke(currentOxygen, maxOxygen);
    }
}