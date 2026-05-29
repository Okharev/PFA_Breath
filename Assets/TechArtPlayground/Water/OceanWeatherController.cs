using System.Collections;
using TechArtPlayground.Wind;
using UnityEngine;

namespace TechArtPlayground.Water
{
    [System.Serializable]
    public struct OceanWeatherPreset
    {
        [Header("Simulation Parameters")]
        [Tooltip("Speed of the wind driving the waves")]
        public float windSpeed;
        [Tooltip("Overall height/energy of the waves")]
        public float phillipsAmplitude;
        [Tooltip("Sharpness of the wave crests")]
        [Range(0f, 2f)] public float choppiness;

        [Header("Material Parameters")]
        [ColorUsage(false, true)] public Color shallowColor;
        [ColorUsage(false, true)] public Color deepColor;
        
        [Tooltip("Lower values create more foam at the crests")]
        [Range(-1f, 1f)] public float foamBias;
        [Tooltip("Controls the sharpness/clumping of the foam")]
        [Range(0.1f, 5f)] public float foamPower;
    }

    [ExecuteAlways]
    [RequireComponent(typeof(OceanFFTBinder))]
    public class OceanWeatherController : MonoBehaviour
    {
        [Header("Dependencies")]
        public OceanFFTBinder fftBinder;
        public Material oceanMaterial;

        [Header("Weather Presets")]
        public OceanWeatherPreset calmPreset = new OceanWeatherPreset
        {
            windSpeed = 5.0f,
            phillipsAmplitude = 0.0005f,
            choppiness = 0.8f,
            shallowColor = new Color(0.2f, 0.6f, 0.7f, 1.0f),
            deepColor = new Color(0.02f, 0.1f, 0.2f, 1.0f),
            foamBias = 0.5f,
            foamPower = 2.0f
        };

        public OceanWeatherPreset tempestPreset = new OceanWeatherPreset
        {
            windSpeed = 35.0f,
            phillipsAmplitude = 0.015f,
            choppiness = 1.8f,
            shallowColor = new Color(0.1f, 0.3f, 0.35f, 1.0f),
            deepColor = new Color(0.01f, 0.05f, 0.1f, 1.0f),
            foamBias = -0.3f,
            foamPower = 1.0f
        };

        [Header("Current State")]
        [Range(0f, 1f)]
        [Tooltip("0 = Calm, 1 = Tempest. You can drag this manually to test the blend!")]
        public float weatherBlend = 0f;

        // Cached Property IDs for performance
        private static readonly int ShallowColor = Shader.PropertyToID("_ShallowColor");
        private static readonly int DeepColor = Shader.PropertyToID("_DeepColor");
        private static readonly int FoamBias = Shader.PropertyToID("_FoamBias");
        private static readonly int FoamPower = Shader.PropertyToID("_FoamPower");

        private Coroutine transitionCoroutine;

        void Reset()
        {
            fftBinder = GetComponent<OceanFFTBinder>();
        }

        void Update()
        {
            if (fftBinder == null || oceanMaterial == null) return;

            ApplyWeatherBlend();
        }

        /// <summary>
        /// Continuously applies the interpolated values based on the weatherBlend slider.
        /// </summary>
        private void ApplyWeatherBlend()
        {
            float currentWindSpeed = Mathf.Lerp(calmPreset.windSpeed, tempestPreset.windSpeed, weatherBlend);

            // =========================================================
            // NEW: CASCADE WEATHER TO THE ENTIRE WORLD
            // =========================================================
            if (GlobalWindManager.Instance != null)
            {
                // Grab the current direction, fallback to Vector3.right if it's zero
                Vector3 currentDir = GlobalWindManager.Instance.windVelocity.normalized;
                if (currentDir == Vector3.zero) currentDir = Vector3.right;
                
                // Update the global manager so banners and chimes react to the Tempest!
                GlobalWindManager.Instance.windVelocity = currentDir * currentWindSpeed;
            }

            // 2. Lerp FFT Compute Parameters (Syncing speed locally as well)
            fftBinder.windSpeed = currentWindSpeed;
            fftBinder.phillipsAmplitude = Mathf.Lerp(calmPreset.phillipsAmplitude, tempestPreset.phillipsAmplitude, weatherBlend);
            fftBinder.choppiness = Mathf.Lerp(calmPreset.choppiness, tempestPreset.choppiness, weatherBlend);

            // 3. Lerp URP Material Parameters
            oceanMaterial.SetColor(ShallowColor, Color.Lerp(calmPreset.shallowColor, tempestPreset.shallowColor, weatherBlend));

            // 2. Lerp URP Material Parameters
            oceanMaterial.SetColor(ShallowColor, Color.Lerp(calmPreset.shallowColor, tempestPreset.shallowColor, weatherBlend));
            oceanMaterial.SetColor(DeepColor, Color.Lerp(calmPreset.deepColor, tempestPreset.deepColor, weatherBlend));
            oceanMaterial.SetFloat(FoamBias, Mathf.Lerp(calmPreset.foamBias, tempestPreset.foamBias, weatherBlend));
            oceanMaterial.SetFloat(FoamPower, Mathf.Lerp(calmPreset.foamPower, tempestPreset.foamPower, weatherBlend));
        }

        // --- Public API for Gameplay Scripts ---

        [ContextMenu("Transition To Tempest")]
        public void TriggerTempest() => TransitionWeather(1f, 5f); // 10-second default transition

        [ContextMenu("Transition To Calm")]
        public void TriggerCalm() => TransitionWeather(0f, 10f);

        /// <summary>
        /// Smoothly transitions the weather over a specified duration.
        /// </summary>
        public void TransitionWeather(float targetBlend, float durationInSeconds)
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            transitionCoroutine = StartCoroutine(WeatherTransitionRoutine(targetBlend, durationInSeconds));
        }

        private IEnumerator WeatherTransitionRoutine(float targetBlend, float duration)
        {
            float startBlend = weatherBlend;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // Use smoothstep for a more natural, non-linear easing transition
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                weatherBlend = Mathf.Lerp(startBlend, targetBlend, t);
                yield return null;
            }

            weatherBlend = targetBlend;
        }
    }
}