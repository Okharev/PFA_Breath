using UnityEngine;

namespace TechArtPlayground
{
    public class BoidAttractor : MonoBehaviour
    {
        [Tooltip("Strength of the pull. You can expose this to have unique weights per attractor!")]
        public float weight = 1.0f;

        private void OnEnable()
        {
            if (BoidsManager.Instance != null) BoidsManager.Instance.RegisterAttractor(this);
        }

        private void OnDisable()
        {
            if (BoidsManager.Instance != null) BoidsManager.Instance.UnregisterAttractor(this);
        }
    }
}