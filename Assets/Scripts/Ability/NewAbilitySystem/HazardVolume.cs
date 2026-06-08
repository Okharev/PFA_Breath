using System.Collections.Generic;
using UnityEngine;

namespace Ability.NewAbilitySystem
{
    [RequireComponent(typeof(Collider))]
    public class HazardVolume : MonoBehaviour
    {
        [Tooltip("Who owns this hazard? (Set dynamically by projectiles, or leave null for environment traps)")]
        public GameObject Source;

        [Tooltip("Should this trigger automatically on physics collision? (Turn OFF for projectiles, ON for traps)")]
        public bool triggerOnPhysicsEnter;

        [SerializeReference, SubclassSelector]
        public List<IAbilityEffect> effects = new();

        // Used strictly for static environmental traps (mines, acid pools)
        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnPhysicsEnter) ApplyTo(other.gameObject);
        }

        /// <summary>
        ///     Applies all stored effects to the victim.
        /// </summary>
        public void ApplyTo(GameObject victim)
        {
            // Re-use your existing Context struct!
            AbilityContext context = new(
                null,
                Source != null ? Source : gameObject,
                target: victim,
                targetPosition: victim.transform.position
            );

            // Execute Effects safely
            foreach (IAbilityEffect effect in effects) effect?.Execute(context);
        }
    }
}