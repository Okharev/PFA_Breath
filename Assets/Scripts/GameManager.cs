using System;
using UnityEngine;

public enum GameMode
{
    Exploration,
    Combat
}

/// <summary>
/// Manages the high-level state of the game.
/// Implements the Singleton and Observer patterns.
/// </summary>
[DefaultExecutionOrder(-60)] // Runs before TurnManager
public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }

    public GameMode CurrentMode { get; private set; } = GameMode.Exploration;

    public static event Action<GameMode> OnGameModeChanged;

    private void Awake()
    {
        if (!Instance) Instance = this;
        else Destroy(gameObject);
    }

    public void SwitchToCombat()
    {
        if (CurrentMode == GameMode.Combat) return;
        
        CurrentMode = GameMode.Combat;
        OnGameModeChanged?.Invoke(CurrentMode);
    }

    public void SwitchToExploration()
    {
        if (CurrentMode == GameMode.Exploration) return;
        
        CurrentMode = GameMode.Exploration;
        OnGameModeChanged?.Invoke(CurrentMode);
    }
}