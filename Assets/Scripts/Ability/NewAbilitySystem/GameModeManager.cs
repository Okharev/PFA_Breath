using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ability.NewAbilitySystem
{
    public interface IInteractable
    {
        void Interact(GameObject instigator);
    }

    /// <summary>
    ///     Defines the high-level states of the game loop.
    /// </summary>
    public enum GameMode
    {
        Exploration, // Real-time, free movement
        Combat // Paused, turn-based discrete actions
    }

    /// <summary>
    ///     Manages the current game state and broadcasts transitions to other systems.
    /// </summary>
    // Execution order ensures this initializes before the TurnManager (-50)
    [DefaultExecutionOrder(-100)]
    public class GameModeManager : MonoBehaviour
    {
        [field: SerializeField]
        [field: Tooltip("The current active game mode.")]
        public GameMode CurrentMode { get; private set; } = GameMode.Exploration;

        public static GameModeManager Instance { get; private set; }

        public static void CleanAllHazards(GameMode mode = GameMode.Exploration)
        {
            HazardVolume[] allHazards = FindObjectsByType<HazardVolume>();

            foreach (HazardVolume hazard in allHazards)
            {
                Destroy(hazard.gameObject);
            }
        }
        
        private void Awake()
        {
            // Standard Singleton enforcement
            if (Instance == null)
            {
                Instance = this;
                // Uncomment the line below if this manager needs to persist across scene loads
                // DontDestroyOnLoad(gameObject); 
            }
            else
            {
                Debug.LogWarning("[GameModeManager] Duplicate instance destroyed on Awake.");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Broadcast the initial state so systems like TurnManager can sync up on launch
            OnGameModeChanged?.Invoke(CurrentMode);

        }

        // private void OnEnable()
        // {
        //     OnGameModeChanged += CleanAllHazards;
        // }

        // private void OnDisable()
        // {
        //     OnGameModeChanged -= CleanAllHazards;
        // }

#if UNITY_EDITOR
        private void Update()
        {
            // Use the fully qualified path so you don't need to add the using statement globally
            if (Keyboard.current != null &&
                Keyboard.current.f1Key.wasPressedThisFrame)
                SetGameMode(CurrentMode == GameMode.Exploration ? GameMode.Combat : GameMode.Exploration);
        }
#endif

        /// <summary>
        ///     Fired immediately when the game mode transitions to a new state.
        /// </summary>
        public static event Action<GameMode> OnGameModeChanged;

        /// <summary>
        ///     Requests a transition to a new game mode.
        /// </summary>
        /// <param name="newMode">The target GameMode to transition into.</param>
        public void SetGameMode(GameMode newMode)
        {
            if (CurrentMode == newMode)
            {
                Debug.Log($"[GameModeManager] Already in {newMode} mode. Transition ignored.");
                return;
            }

            GameMode previousMode = CurrentMode;
            CurrentMode = newMode;

            Debug.Log($"[GameModeManager] Transitioning from {previousMode} to {CurrentMode}");

            // Notify all subscribed listeners (like the TurnManager)
            OnGameModeChanged?.Invoke(CurrentMode);
        }
    }
}