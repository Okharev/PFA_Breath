using TechArtPlayground.Wind;
using UnityEngine;
using R3;

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

        private static readonly int ShallowColor = Shader.PropertyToID("_ShallowColor");
        private static readonly int DeepColor = Shader.PropertyToID("_DeepColor");
        private static readonly int FoamBias = Shader.PropertyToID("_FoamBias");
        private static readonly int FoamPower = Shader.PropertyToID("_FoamPower");

        // R3 Bridge
        private readonly ReactiveProperty<float> _weatherBlendRx = new(0f);
        private DisposableBag _disposables;

        private void Reset() => fftBinder = GetComponent<OceanFFTBinder>();

        private void OnEnable()
        {
            _disposables = new DisposableBag();

            // 1. Subscribe to R3 stream with a Distinct filter
            _weatherBlendRx
                .DistinctUntilChanged()
                .Subscribe(ApplyWeatherBlend)
                .AddTo(ref _disposables);

            // 2. Bridge standard C# Event to R3
            if (GlobalWeatherManager.Instance != null)
            {
                GlobalWeatherManager.Instance.OnWeatherBlendChanged += OnGlobalWeatherChanged;
                _weatherBlendRx.Value = GlobalWeatherManager.Instance.currentBlend;
            }
        }

        // Intercepts traditional C# event and pushes to reactive stream
        private void OnGlobalWeatherChanged(float blend) => _weatherBlendRx.Value = blend;

        private void ApplyWeatherBlend(float blend)
        {
            if (fftBinder == null || oceanMaterial == null) return;

            // Push to FFT Binder's R3 streams
            fftBinder.SetWindSpeed(Mathf.Lerp(calmPreset.windSpeed, tempestPreset.windSpeed, blend));
            fftBinder.SetPhillipsAmplitude(Mathf.Lerp(calmPreset.phillipsAmplitude, tempestPreset.phillipsAmplitude, blend));
            fftBinder.SetChoppiness(Mathf.Lerp(calmPreset.choppiness, tempestPreset.choppiness, blend));

            // Push to Material
            oceanMaterial.SetColor(ShallowColor, Color.Lerp(calmPreset.shallowColor, tempestPreset.shallowColor, blend));
            oceanMaterial.SetColor(DeepColor, Color.Lerp(calmPreset.deepColor, tempestPreset.deepColor, blend));
            oceanMaterial.SetFloat(FoamBias, Mathf.Lerp(calmPreset.foamBias, tempestPreset.foamBias, blend));
            oceanMaterial.SetFloat(FoamPower, Mathf.Lerp(calmPreset.foamPower, tempestPreset.foamPower, blend));
        }

        private void OnDisable()
        {
            if (GlobalWeatherManager.Instance != null)
                GlobalWeatherManager.Instance.OnWeatherBlendChanged -= OnGlobalWeatherChanged;

            _disposables.Dispose();
        }
    }
}