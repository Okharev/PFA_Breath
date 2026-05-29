using UnityEngine;

namespace ChimeTest
{
    [ExecuteAlways]
    public class GlobalWindController : MonoBehaviour
    {
        [Header("Wind Vectors")]
        public Vector3 windDirection = new Vector3(1, 0, 0);
        [Range(0f, 5f)] public float windStrength = 1.0f;

        [Header("Global Gust Texture")]
        [Tooltip("A seamless, low-frequency Perlin/Simplex noise texture.")]
        public Texture2D windNoiseTexture;
        [Tooltip("How large the wind gusts are in world space (e.g., 50 meters).")]
        public float windScale = 50.0f;
        [Tooltip("How fast the texture pans across the world.")]
        public float windSpeed = 2.0f;

        private Vector2 _currentPanOffset;

        void Update()
        {
            // Normalize direction to ensure predictable panning
            Vector3 normDir = windDirection.normalized;
            
            // Calculate texture panning based on time, speed, and direction
            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
            _currentPanOffset += new Vector2(normDir.x, normDir.z) * (windSpeed * dt);

            // Pass standard properties
            Shader.SetGlobalVector("_GlobalWindDirection", normDir);
            Shader.SetGlobalFloat("_GlobalWindStrength", windStrength);

            // Pass texture and mapping properties
            if (windNoiseTexture != null)
            {
                Shader.SetGlobalTexture("_GlobalWindNoise", windNoiseTexture);
                
                // Pack scale and offset into a single Vector4 for efficient HLSL unpacking
                // X, Y = Offset | Z = Inverse Scale (for cheaper multiplication in shader)
                Shader.SetGlobalVector("_GlobalWindMapping", new Vector4(
                    _currentPanOffset.x, 
                    _currentPanOffset.y, 
                    1.0f / Mathf.Max(windScale, 0.1f), 
                    0.0f
                ));
            }
        }
    }
}