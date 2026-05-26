using UnityEngine;

namespace TechArtPlayground
{
    public class BoidAttractor : MonoBehaviour
    {
        [Tooltip("The specific swarm this attractor influences.")]
        public BoidSwarm targetSwarm;

        [Tooltip("Strength of the pull. You can expose this to have unique weights per attractor!")]
        public float weight = 1.0f;

        private void OnEnable()
        {
            if (targetSwarm != null) targetSwarm.RegisterAttractor(this);
        }

        private void OnDisable()
        {
            if (targetSwarm != null) targetSwarm.UnregisterAttractor(this);
        }
    }
}