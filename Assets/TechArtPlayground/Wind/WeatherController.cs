using UnityEngine;

namespace TechArtPlayground.Wind
{
    [DefaultExecutionOrder(-100)] // Runs first to update physics/shaders
    [ExecuteAlways]
    public class WeatherManager : MonoBehaviour
    {
        private static readonly int GlobalWindVelocity = Shader.PropertyToID("_GlobalWindVelocity");
        private static readonly int GlobalWindTurbulence = Shader.PropertyToID("_GlobalWindTurbulence");
        public static WeatherManager Instance { get; private set; }

        [Header("Global Weather Settings")]
        [Tooltip("The direction the wind is blowing. Will be normalized automatically.")]
        public Vector3 windDirection = new Vector3(1f, 0f, 1f);

        [Tooltip("The base strength of the wind across the world.")]
        public float windIntensity = 5f;

        [Tooltip("How chaotic the wind is (affects cloth ripples and chime swings).")]
        [Range(0f, 5f)]
        public float windGusts = 1.5f;

        // Calculates the final velocity vector for the legacy systems
        public Vector3 CurrentWindVelocity => windDirection.normalized * windIntensity;

        private void OnEnable()
        {
            Instance = this;
        }

        private void Update()
        {
            // Push to global shaders for Banners and Chimes
            Shader.SetGlobalVector(GlobalWindVelocity, CurrentWindVelocity);
            Shader.SetGlobalFloat(GlobalWindTurbulence, windGusts);
        }

        private void OnDrawGizmos()
        {
            Vector3 finalVelocity = CurrentWindVelocity;
            if (finalVelocity.sqrMagnitude < 0.01f) return;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + finalVelocity;

            Gizmos.DrawLine(startPos, endPos);
            Gizmos.DrawSphere(startPos, 0.2f);

            // Draw Arrowhead
            Vector3 direction = finalVelocity.normalized;
            float arrowHeadLength = Mathf.Clamp(finalVelocity.magnitude * 0.15f, 0.3f, 2f);

            Quaternion lookRot = Quaternion.LookRotation(direction);
            Vector3 rightWing = lookRot * Quaternion.Euler(0, 150, 0) * Vector3.forward;
            Vector3 leftWing = lookRot * Quaternion.Euler(0, -150, 0) * Vector3.forward;
            Vector3 upWing = lookRot * Quaternion.Euler(150, 0, 0) * Vector3.forward;
            Vector3 downWing = lookRot * Quaternion.Euler(-150, 0, 0) * Vector3.forward;

            Gizmos.DrawLine(endPos, endPos + rightWing * arrowHeadLength);
            Gizmos.DrawLine(endPos, endPos + leftWing * arrowHeadLength);
            Gizmos.DrawLine(endPos, endPos + upWing * arrowHeadLength);
            Gizmos.DrawLine(endPos, endPos + downWing * arrowHeadLength);
        }
    }
}