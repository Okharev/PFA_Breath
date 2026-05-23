using UnityEngine;

namespace Ability
{
    /// <summary>
    ///     Packages all necessary game state required for an ability to execute.
    ///     Keeps the IAbility signature clean and allows future expansion (e.g., adding TargetEntity)
    /// </summary>
    public struct AbilityContext
    {
        public GameObject Caster;
        public Vector3 CasterPosition;
        public Vector3? MouseWorldPosition;
        public ActionVisualizer Visualizer;
    }

    public interface IAbility
    {
        string AbilityId { get; }
        int TurnCost { get; }
        int OxygenCost { get; }
        bool RequiresTargeting { get; }

        int CurrentLevel { get; }
        void SetLevel(int newLevel);
        
        // --- Standardized State Exposure ---
        int CurrentCooldown { get; }
        int MaxCooldown { get; }
        int CurrentChannelTime { get; }
        int RequiredChannelTime { get; }
        
        // Expose state for UI/AI evaluation
        bool IsChanneling { get; }
        bool IsReady { get; } 

        bool CanExecute(AbilityContext context);
        void DrawPreview(AbilityContext context);
        void Execute(AbilityContext context);
    }
}