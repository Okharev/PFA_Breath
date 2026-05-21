using UnityEngine;

namespace Ability
{
    /// <summary>
    ///     Packages all necessary game state required for an ability to execute.
    ///     Keeps the IAbility signature clean and allows future expansion (e.g., adding TargetEntity)
    ///     without breaking existing implementations.
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

        /// <summary>
        ///     If true, the ability requires a ground target (e.g., Shooting, Teleport).
        ///     If false, it executes instantly without aiming (e.g., Self-Heal, Nova).
        /// </summary>
        bool RequiresTargeting { get; }

        bool CanExecute(AbilityContext context);

        /// <summary>
        ///     Called every frame while the player is aiming.
        ///     The ability decides how it wants to be visualized.
        /// </summary>
        void DrawPreview(AbilityContext context);

        /// <summary>
        ///     The actual execution logic of the ability.
        /// </summary>
        void Execute(AbilityContext context);
    }
}