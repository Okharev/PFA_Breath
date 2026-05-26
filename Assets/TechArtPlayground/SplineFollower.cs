using UnityEngine;
using UnityEngine.Splines;

/// <summary>
///     Moves an object along a 3D Spline with Perlin noise speed variation
///     and random starting position logic.
/// </summary>
public class SplineFollower : MonoBehaviour
{
    [Header("Spline Settings")] [SerializeField]
    private SplineContainer splineContainer;

    [Header("Movement Settings")] [SerializeField]
    private float baseSpeed = 5f;

    [SerializeField] private float minSpeed = 2f;
    [SerializeField] private float maxSpeed = 10f;

    [Header("Noise Settings")] [SerializeField]
    private float noiseFrequency = 1f;

    [SerializeField] private float noiseSeed;

    private float _distanceTraveled;
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

        // Initialize position to a random point
        InitializeRandomPosition();
    }

    private void Update()
    {
        // 1. Calculate Perlin Noise
        float noiseValue = Mathf.PerlinNoise(Time.time * noiseFrequency, noiseSeed);
        float currentSpeed = Mathf.Lerp(minSpeed, maxSpeed, noiseValue);

        // 2. Increment distance
        _distanceTraveled = (_distanceTraveled + currentSpeed * Time.deltaTime) % _splineLength;

        // 3. Update Position/Rotation
        UpdateTransform(_distanceTraveled / _splineLength);
    }

    private void InitializeRandomPosition()
    {
        // Randomly pick a distance along the spline
        _distanceTraveled = Random.Range(0f, _splineLength);

        // Update transform immediately so the object doesn't flicker at (0,0,0) 
        // for one frame before the first Update call.
        UpdateTransform(_distanceTraveled / _splineLength);
    }

    private void UpdateTransform(float t)
    {
        Vector3 position = splineContainer.EvaluatePosition(t);
        Vector3 tangent = splineContainer.EvaluateTangent(t);

        transform.position = position;

        if (tangent != Vector3.zero) transform.rotation = Quaternion.LookRotation(tangent);
    }
}