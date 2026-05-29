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
        
        /// <summary>
        /// Returns a target point clamped to a specific Y-height.
        /// Useful for keeping LineRenderers and Previews perfectly level.
        /// </summary>
        public Vector3 GetPlanarTarget(float yHeight)
        {
            if (!MouseWorldPosition.HasValue) return CasterPosition;
            
            Vector3 target = MouseWorldPosition.Value;
            target.y = yHeight; // Clamp to the provided height (e.g., firePoint.y)
            return target;
        }

        /// <summary>
        /// Calculates a normalized direction vector strictly on the XZ plane.
        /// </summary>
        public Vector3 GetPlanarAimDirection(Vector3 originPos)
        {
            if (!MouseWorldPosition.HasValue) return Vector3.forward;

            Vector3 targetPos = MouseWorldPosition.Value;
            targetPos.y = originPos.y; // Flatten the target to the origin's height
            
            return (targetPos - originPos).normalized;
        }
    }

    public interface IAbility
    {
        string AbilityId { get; }
        int TurnCost { get; }
        int OxygenCost { get; }
        bool RequiresTargeting { get; }

        int CurrentLevel { get; }

        // --- Standardized State Exposure ---
        int CurrentCooldown { get; }
        int MaxCooldown { get; }
        int CurrentChannelTime { get; }
        int RequiredChannelTime { get; }

        // Expose state for UI/AI evaluation
        bool IsChanneling { get; }
        bool IsReady { get; }
        void SetLevel(int newLevel);

        bool CanExecute(AbilityContext context);
        void DrawPreview(AbilityContext context);
        void Execute(AbilityContext context);
    }

    public interface IWeaponAbility : IAbility
    {
        int CurrentAmmo { get; }
        int MaxAmmo { get; }
        int ReloadTurnCost { get; }
        bool NeedsReload { get; }

        /// <summary>
        ///     Refills the weapon's magazine.
        /// </summary>
        void Reload();
    }
}