using System;
using System.Collections;
using UnityEngine;

namespace TechArtPlayground.Wind
{
    [System.Serializable]
    public struct GlobalWindPreset
    {
        public Vector3 windDirection;
        public float windIntensity;
        [Range(0f, 5f)] public float windGusts;
    }

    [DefaultExecutionOrder(-100)] 
    [ExecuteAlways]
    public class GlobalWeatherManager : MonoBehaviour
    {
        public static GlobalWeatherManager Instance { get; private set; }

        private static readonly int GlobalWindVelocity = Shader.PropertyToID("_GlobalWindVelocity");
        private static readonly int GlobalWindTurbulence = Shader.PropertyToID("_GlobalWindTurbulence");

        [Header("Global Weather Presets")]
        [SerializeField] private GlobalWindPreset calmPreset = new GlobalWindPreset { windDirection = new Vector3(1,0,1), windIntensity = 5f, windGusts = 1.5f };
        [SerializeField] private GlobalWindPreset tempestPreset = new GlobalWindPreset { windDirection = new Vector3(1,0,1), windIntensity = 35f, windGusts = 4.0f };

        [Header("Current State")]
        [Range(0f, 1f)]
        public float currentBlend = 0f;
        private float _lastBlend = -1f;

        // OBSERVER PATTERN: Subsystems subscribe to this event to react to weather changes.
        public event Action<float> OnWeatherBlendChanged;

        private Coroutine transitionCoroutine;

        private void OnEnable()
        {
            // Standard Singleton implementation
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }
        
        private void Start()
        {
            // FIX: Ensure the initial state is calculated and cached on startup!
            ApplyBlend(currentBlend);
            _lastBlend = currentBlend;
        }

        private static readonly int GlobalUnscaledTime = Shader.PropertyToID("_GlobalUnscaledTime");
        
        private void Update()
        {
            Shader.SetGlobalFloat(GlobalUnscaledTime, Time.unscaledTime);
            
            // OBSERVER/POLLING: If the blend value changes (either by the Coroutine 
            // OR by you dragging the slider in the Inspector), apply the new weather.
            if (Mathf.Abs(currentBlend - _lastBlend) > 0.0001f)
            {
                ApplyBlend(currentBlend);
                _lastBlend = currentBlend;
            }
        }
        
        /// <summary>
        /// Triggers a smooth weather transition to a specific blend percentage.
        /// </summary>
        /// <param name="targetBlend">0f for Calm, 1f for Tempest.</param>
        /// <param name="duration">Time in seconds for the transition to complete.</param>
        public void TransitionToBlend(float targetBlend, float duration = 5f)
        {
            // Clamp to ensure we don't pass invalid values to the shader
            StartTransition(Mathf.Clamp01(targetBlend), duration);
        }

        public void TransitionToTempest(float duration = 5f) => StartTransition(1f, duration);
        public void TransitionToCalm(float duration = 10f) => StartTransition(0f, duration);

        private void StartTransition(float targetBlend, float duration)
        {
            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
            transitionCoroutine = StartCoroutine(WeatherTransitionRoutine(targetBlend, duration));
        }

        private IEnumerator WeatherTransitionRoutine(float targetBlend, float duration)
        {
            float startBlend = currentBlend;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // We only update the float here. The Update() loop handles the rest automatically!
                currentBlend = Mathf.SmoothStep(startBlend, targetBlend, elapsed / duration);
                yield return null;
            }

            currentBlend = targetBlend;
            transitionCoroutine = null;
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Unity triggers this whenever ANY variable is changed in the Inspector.
            // We use a slight delay to ensure Unity is done deserializing the new values
            // before we try pushing them to the GPU and other scripts.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return; // Prevent errors if the object was destroyed
                
                ForceUpdateWeather();
            };
        }
#endif

        /// <summary>
        /// Manually forces the weather to recalculate and broadcast to all listeners.
        /// Useful if you change preset values via code during runtime.
        /// </summary>
        public void ForceUpdateWeather()
        {
            ApplyBlend(currentBlend);
            _lastBlend = currentBlend;
        }

        public Vector3 CurrentWindVelocity { get; private set; }
        public float CurrentWindTurbulence { get; private set; }

        private void ApplyBlend(float blend)
        {
            Vector3 currentDir = Vector3.Slerp(calmPreset.windDirection.normalized, tempestPreset.windDirection.normalized, blend);
            float currentIntensity = Mathf.Lerp(calmPreset.windIntensity, tempestPreset.windIntensity, blend);
    
            // Cache the results publicly for O(1) retrieval by Compute Shaders
            CurrentWindTurbulence = Mathf.Lerp(calmPreset.windGusts, tempestPreset.windGusts, blend);
            CurrentWindVelocity = currentDir * currentIntensity;

            Shader.SetGlobalVector(GlobalWindVelocity, CurrentWindVelocity);
            Shader.SetGlobalFloat(GlobalWindTurbulence, CurrentWindTurbulence);

            OnWeatherBlendChanged?.Invoke(blend);
        }
    }
}