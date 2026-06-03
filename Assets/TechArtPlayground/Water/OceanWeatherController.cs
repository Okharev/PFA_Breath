using TechArtPlayground.Wind;
using UnityEngine;

namespace TechArtPlayground.Water
{
    // Preset struct remains the same...
    [System.Serializable]
    public struct OceanWeatherPreset
    {
        public float windSpeed;
        public float phillipsAmplitude;
        [Range(0f, 2f)] public float choppiness;
        [ColorUsage(false, true)] public Color shallowColor;
        [ColorUsage(false, true)] public Color deepColor;
        [Range(-1f, 1f)] public float foamBias;
        [Range(0.1f, 5f)] public float foamPower;
    }

    [ExecuteAlways]
    [RequireComponent(typeof(OceanFFTBinder))]
    public class OceanWeatherController : MonoBehaviour
    {
        [Header("Dependencies")]
        public OceanFFTBinder fftBinder;
        public Material oceanMaterial;

        [Header("Local Weather Presets")]
        public OceanWeatherPreset calmPreset;
        public OceanWeatherPreset tempestPreset;

        private static readonly int ShallowColor = Shader.PropertyToID("_ShallowColor");
        private static readonly int DeepColor = Shader.PropertyToID("_DeepColor");
        private static readonly int FoamBias = Shader.PropertyToID("_FoamBias");
        private static readonly int FoamPower = Shader.PropertyToID("_FoamPower");

        private void Reset() => fftBinder = GetComponent<OceanFFTBinder>();

        private void OnEnable()
        {
            // OBSERVER: Subscribe to the global weather changes
            if (GlobalWeatherManager.Instance != null)
            {
                GlobalWeatherManager.Instance.OnWeatherBlendChanged += ApplyWeatherBlend;
                
                // Initialize to current state
                ApplyWeatherBlend(GlobalWeatherManager.Instance.currentBlend);
            }
        }

        private void OnDisable()
        {
            // CRITICAL: Always unsubscribe to prevent memory leaks
            if (GlobalWeatherManager.Instance != null)
            {
                GlobalWeatherManager.Instance.OnWeatherBlendChanged -= ApplyWeatherBlend;
            }
        }

        private void ApplyWeatherBlend(float blend)
        {
            if (fftBinder == null || oceanMaterial == null) return;

            // 1. Lerp FFT Compute Parameters
            fftBinder.windSpeed = Mathf.Lerp(calmPreset.windSpeed, tempestPreset.windSpeed, blend);
            fftBinder.phillipsAmplitude = Mathf.Lerp(calmPreset.phillipsAmplitude, tempestPreset.phillipsAmplitude, blend);
            fftBinder.choppiness = Mathf.Lerp(calmPreset.choppiness, tempestPreset.choppiness, blend);

            // 2. Lerp URP Material Parameters
            oceanMaterial.SetColor(ShallowColor, Color.Lerp(calmPreset.shallowColor, tempestPreset.shallowColor, blend));
            oceanMaterial.SetColor(DeepColor, Color.Lerp(calmPreset.deepColor, tempestPreset.deepColor, blend));
            oceanMaterial.SetFloat(FoamBias, Mathf.Lerp(calmPreset.foamBias, tempestPreset.foamBias, blend));
            oceanMaterial.SetFloat(FoamPower, Mathf.Lerp(calmPreset.foamPower, tempestPreset.foamPower, blend));
        }
    }
}