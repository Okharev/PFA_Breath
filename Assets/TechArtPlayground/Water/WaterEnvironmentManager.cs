using UnityEngine;

namespace TechArtPlayground.Water
{
    /// <summary>
    /// Manages global water state and pushes configuration to the URP Shader pipeline.
    /// </summary>
    [ExecuteAlways] // Ensures waves preview correctly in the editor
    public class WaterEnvironmentManager : MonoBehaviour
    {
        [SerializeField] private WaterProfile currentWaterProfile;

        // Pre-hash property IDs for O(1) string lookups in the render loop.
        private static readonly int WaveSpeedId = Shader.PropertyToID("_GlobalWaveSpeed");
        private static readonly int WaveHeightId = Shader.PropertyToID("_GlobalWaveHeight");
        private static readonly int WaveFrequencyId = Shader.PropertyToID("_GlobalWaveFrequency");
        private static readonly int WaveDirectionId = Shader.PropertyToID("_GlobalWaveDirection");
        private static readonly int WaterColorId = Shader.PropertyToID("_GlobalWaterColor");
        private static readonly int FoamColorId = Shader.PropertyToID("_GlobalFoamColor");

        private void Update()
        {
            if (currentWaterProfile is null) return;
        
            UpdateGlobalShaderVariables();
        }

        /// <summary>
        /// Pushes the CPU-side data parameters to the GPU.
        /// Time Complexity: O(1)
        /// </summary>
        private void UpdateGlobalShaderVariables()
        {
            Shader.SetGlobalFloat(WaveSpeedId, currentWaterProfile.waveSpeed);
            Shader.SetGlobalFloat(WaveHeightId, currentWaterProfile.waveHeight);
            Shader.SetGlobalFloat(WaveFrequencyId, currentWaterProfile.waveFrequency);
            Shader.SetGlobalVector(WaveDirectionId, currentWaterProfile.waveDirection);
            Shader.SetGlobalColor(WaterColorId, currentWaterProfile.waterColor);
            Shader.SetGlobalColor(FoamColorId, currentWaterProfile.foamColor);
        }
    
        // Optional: Public method to smoothly transition profiles over time
        public void TransitionToProfile(WaterProfile newProfile, float transitionDuration)
        {
            // Implementation for a coroutine or DOTween transition here
            // to lerp between the current and new profile values.
        }
    }
}