using UnityEngine;

namespace Ability.NewAbilitySystem
{

    public readonly struct AbilityContext
    {
        public readonly GameObject Source;
        public readonly GameObject Target;
        public readonly Vector3 TargetPosition;

        public AbilityContext(GameObject source, GameObject target = null, Vector3 targetPosition = default)
        {
            Source = source;
            Target = target;
            TargetPosition = targetPosition;
        }
    }
    
    public interface IAbilityCondition
    {
        /// <summary>
        /// Evaluates if the ability can be cast given the current context.
        /// </summary>
        bool CanExecute(AbilityContext context);
    }

    public interface IAbilityEffect
    {
        /// <summary>
        /// Executes the specific logic of the ability.
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

        public static ConditionResult Success() => new ConditionResult(true, FailureReason.None);
        public static ConditionResult Fail(FailureReason reason) => new ConditionResult(false, reason);

        private ConditionResult(bool isMet, FailureReason reason)
        {
            IsMet = isMet;
            Reason = reason;
        }
    }
}