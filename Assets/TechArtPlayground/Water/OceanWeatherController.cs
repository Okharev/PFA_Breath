using TechArtPlayground.Wind;
using UnityEngine;

namespace TechArtPlayground.Water
{
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

        private static readonly int ShallowColorID = Shader.PropertyToID("_ShallowColor");
        private static readonly int DeepColorID = Shader.PropertyToID("_DeepColor");
        private static readonly int FoamBiasID = Shader.PropertyToID("_FoamBias");
        private static readonly int FoamPowerID = Shader.PropertyToID("_FoamPower");

        private float _currentBlend = -1f; // Initialized out-of-bounds to force the first update

        private void Reset() => fftBinder = GetComponent<OceanFFTBinder>();

        private void OnEnable()
        {
            if (GlobalWeatherManager.Instance != null)
            {
                // Subscribe directly via standard C# Action
                GlobalWeatherManager.Instance.OnWeatherBlendChanged += ApplyWeatherBlend;
                
                // Force initial state sync
                ApplyWeatherBlend(GlobalWeatherManager.Instance.currentBlend);
            }
        }

        private void ApplyWeatherBlend(float blend)
        {
            // Equality check replaces Rx.DistinctUntilChanged()
            if (Mathf.Approximately(_currentBlend, blend) || fftBinder == null || oceanMaterial == null) return;
            _currentBlend = blend;

            // Push to FFT Binder properties
            fftBinder.SetWindSpeed(Mathf.Lerp(calmPreset.windSpeed, tempestPreset.windSpeed, blend));
            fftBinder.SetPhillipsAmplitude(Mathf.Lerp(calmPreset.phillipsAmplitude, tempestPreset.phillipsAmplitude, blend));
            fftBinder.SetChoppiness(Mathf.Lerp(calmPreset.choppiness, tempestPreset.choppiness, blend));

            // Push to Material
            oceanMaterial.SetColor(ShallowColorID, Color.Lerp(calmPreset.shallowColor, tempestPreset.shallowColor, blend));
            oceanMaterial.SetColor(DeepColorID, Color.Lerp(calmPreset.deepColor, tempestPreset.deepColor, blend));
            oceanMaterial.SetFloat(FoamBiasID, Mathf.Lerp(calmPreset.foamBias, tempestPreset.foamBias, blend));
            oceanMaterial.SetFloat(FoamPowerID, Mathf.Lerp(calmPreset.foamPower, tempestPreset.foamPower, blend));
        }

        private void OnDisable()
        {
            if (GlobalWeatherManager.Instance != null)
            {
                GlobalWeatherManager.Instance.OnWeatherBlendChanged -= ApplyWeatherBlend;
            }
        }
    }
}