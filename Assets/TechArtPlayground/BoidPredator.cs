using UnityEngine;

namespace TechArtPlayground
{
    public class BoidPredator : MonoBehaviour
    {
        [Tooltip("How close the boids need to be before they panic.")]
        public float panicRadius = 5f;

        private void OnEnable()
        {
            if (BoidsManager.Instance != null) BoidsManager.Instance.RegisterPredator(this);
        }

        private void OnDisable()
        {
            if (BoidsManager.Instance != null) BoidsManager.Instance.UnregisterPredator(this);
        }
    }
}