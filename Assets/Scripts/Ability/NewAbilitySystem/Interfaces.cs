using UnityEngine;

namespace Ability.NewAbilitySystem
{
    public struct AbilityContext
    {
        public AbilityData Data;
        
        public readonly GameObject Source;
        public readonly Transform Origin;
        public readonly GameObject Target;
        public readonly Vector3 TargetPosition;

        public AbilityContext(AbilityData data, GameObject source, Transform origin = null, GameObject target = null,
            Vector3 targetPosition = default)
        {
            Data = data;
            Source = source;
            Origin = origin != null ? origin : source.transform;
            Target = target;
            TargetPosition = targetPosition;
        }
    }

    public interface IPreviewableEffect
    {
        /// <summary>
        ///     Asks the effect to draw its intent using the provided drawer.
        /// </summary>
        void DrawPreview(AbilityContext context, IntentDrawer drawer);
    }

    public interface IAbilityCondition
    {
        /// <summary>
        ///     Evaluates if the ability can be cast given the current context.
        /// </summary>
        bool CanExecute(AbilityContext context);
    }

    public interface IAbilityEffect
    {
        /// <summary>
        ///     Executes the specific logic of the ability.
        /// </summary>
        void Execute(AbilityContext context);
    }

    public enum FailureReason
    {
        None,
        NotEnoughAmmo,
        OutOfRange,
        OnCooldown,
        NotEnoughOxygen
    }

    public readonly struct ConditionResult
    {
        public readonly bool IsMet;
        public readonly FailureReason Reason;

        public static ConditionResult Success()
        {
            return new ConditionResult(true, FailureReason.None);
        }

        public static ConditionResult Fail(FailureReason reason)
        {
            return new ConditionResult(false, reason);
        }

        private ConditionResult(bool isMet, FailureReason reason)
        {
            IsMet = isMet;
            Reason = reason;
        }
    }
}