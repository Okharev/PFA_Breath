using UnityEngine;

namespace TechArtPlayground.Wind
{
    [DefaultExecutionOrder(-100)] 
    [ExecuteAlways] 
    public class GlobalWindManager : MonoBehaviour
    {
        private static readonly int GlobalWindVelocityID = Shader.PropertyToID("_GlobalWindVelocity");
        private static readonly int GlobalWindTurbulenceID = Shader.PropertyToID("_GlobalWindTurbulence");

        public static GlobalWindManager Instance { get; private set; }

        [Header("Global Wind Settings")] 
        [Tooltip("Direction and speed of the wind.")]
        [SerializeField] private Vector3 windVelocity = new(5f, 0f, 2f);
        public Vector3 WindVelocity 
        { 
            get => windVelocity; 
            set 
            { 
                if (windVelocity == value) return; 
                windVelocity = value; 
                Shader.SetGlobalVector(GlobalWindVelocityID, windVelocity); 
            } 
        }

        [Tooltip("How chaotic the wind is across the world.")] 
        [Range(0f, 5f)]
        [SerializeField] private float windTurbulence = 1.5f;
        public float WindTurbulence 
        { 
            get => windTurbulence; 
            set 
            { 
                if (Mathf.Approximately(windTurbulence, value)) return; 
                windTurbulence = value; 
                Shader.SetGlobalFloat(GlobalWindTurbulenceID, windTurbulence); 
            } 
        }

        private void OnEnable()
        {
            Instance = this;
            PushGlobals();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensures sliding values in the inspector updates the scene immediately
            PushGlobals();
        }
#endif

        private void PushGlobals()
        {
            Shader.SetGlobalVector(GlobalWindVelocityID, windVelocity);
            Shader.SetGlobalFloat(GlobalWindTurbulenceID, windTurbulence);
        }

        // --- Dessin de la flèche de debug ---
        private void OnDrawGizmos()
        {
            if (windVelocity.sqrMagnitude < 0.01f) return;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);

            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + windVelocity;

            Gizmos.DrawLine(startPos, endPos);
            Gizmos.DrawSphere(startPos, 0.2f);

            Vector3 direction = windVelocity.normalized;
            float arrowHeadLength = Mathf.Clamp(windVelocity.magnitude * 0.15f, 0.3f, 2f);

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