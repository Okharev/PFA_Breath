using UnityEngine;

namespace TechArtPlayground
{
    public class BoidPredator : MonoBehaviour
    {
        [Tooltip("The specific swarm this predator hunts.")]
        public BoidSwarm targetSwarm;

        [Tooltip("How close the boids need to be before they panic.")]
        public float panicRadius = 5f;

        private void OnEnable()
        {
            if (targetSwarm != null) targetSwarm.RegisterPredator(this);
        }

        private void OnDisable()
        {
            if (targetSwarm != null) targetSwarm.UnregisterPredator(this);
        }
    }
}