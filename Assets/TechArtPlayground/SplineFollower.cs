using UnityEngine;
using UnityEngine.Splines;

namespace TechArtPlayground
{
    /// <summary>
    /// Moves an object along a 3D Spline with Perlin noise-based speed variation.
    /// Dependencies: Unity Splines Package.
    /// </summary>
    [RequireComponent(typeof(Transform))]
    public class SplineFollower : MonoBehaviour
    {
        [Header("Spline Settings")]
        [SerializeField] private SplineContainer splineContainer;
    
        [Header("Movement Settings")]
        [SerializeField] private float baseSpeed = 5f;
        [SerializeField] private float minSpeed = 2f;
        [SerializeField] private float maxSpeed = 10f;
    
        [Header("Noise Settings")]
        [SerializeField] private float noiseFrequency = 1f;
        [SerializeField] private float noiseSeed = 0f;

        private float _distanceTraveled = 0f;
        private float _splineLength;

        private void Start()
        {
            if (splineContainer == null)
            {
                Debug.LogError($"{name}: SplineContainer is missing.");
                enabled = false;
                return;
            }

            _splineLength = splineContainer.CalculateLength();
        }

        private void Update()
        {
            // 1. Calculate Perlin Noise for speed modulation
            // We add noiseSeed to offset the sample point, preventing identical movement
            float noiseValue = Mathf.PerlinNoise(Time.time * noiseFrequency, noiseSeed);
        
            // 2. Map noise (0..1) to our Min/Max range
            float currentSpeed = Mathf.Lerp(minSpeed, maxSpeed, noiseValue);

            // 3. Increment distance
            _distanceTraveled += currentSpeed * Time.deltaTime;

            // 4. Wrap distance if we exceed the spline length
            if (_distanceTraveled > _splineLength)
            {
                _distanceTraveled %= _splineLength;
            }

            // 5. Evaluate position and rotation on the spline
            // Evaluate uses normalized time (0 to 1), so we divide by length
            float t = _distanceTraveled / _splineLength;
        
            Vector3 position = splineContainer.EvaluatePosition(t);
            Vector3 tangent = splineContainer.EvaluateTangent(t);
        
            transform.position = position;
        
            // Orient the object to face the direction of movement
            if (tangent != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(tangent);
            }
        }

    }
}