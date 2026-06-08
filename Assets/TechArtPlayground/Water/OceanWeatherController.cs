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

#if UNITY_EDITOR
        [Header("Editor Preview")]
        [Range(0f, 1f)]
        [Tooltip("Scrub this in Edit Mode to preview the weather transition.")]
        public float editorPreviewBlend = 0f;
#endif

        private static readonly int ShallowColorID = Shader.PropertyToID("_ShallowColor");
        private static readonly int DeepColorID = Shader.PropertyToID("_DeepColor");
        private static readonly int FoamBiasID = Shader.PropertyToID("_FoamBias");
        private static readonly int FoamPowerID = Shader.PropertyToID("_FoamPower");

        private float _currentBlend = -1f;

        private void Reset() => fftBinder = GetComponent<OceanFFTBinder>();

        private void OnEnable()
        {
            if (Application.isPlaying && GlobalWeatherManager.Instance != null)
            {
                GlobalWeatherManager.Instance.OnWeatherBlendChanged += ApplyWeatherBlend;
                ApplyWeatherBlend(GlobalWeatherManager.Instance.currentBlend);
            }
            else
            {
                // Force an initial sync in edit mode
                ApplyWeatherBlend(Application.isPlaying ? 0f : editorPreviewBlend);
            }
        }

        private void ApplyWeatherBlend(float blend)
        {
            if (Mathf.Approximately(_currentBlend, blend) || fftBinder == null || oceanMaterial == null) return;
            _currentBlend = blend;

            fftBinder.SetWindSpeed(Mathf.Lerp(calmPreset.windSpeed, tempestPreset.windSpeed, blend));
            fftBinder.SetPhillipsAmplitude(Mathf.Lerp(calmPreset.phillipsAmplitude, tempestPreset.phillipsAmplitude, blend));
            fftBinder.SetChoppiness(Mathf.Lerp(calmPreset.choppiness, tempestPreset.choppiness, blend));

            oceanMaterial.SetColor(ShallowColorID, Color.Lerp(calmPreset.shallowColor, tempestPreset.shallowColor, blend));
            oceanMaterial.SetColor(DeepColorID, Color.Lerp(calmPreset.deepColor, tempestPreset.deepColor, blend));
            oceanMaterial.SetFloat(FoamBiasID, Mathf.Lerp(calmPreset.foamBias, tempestPreset.foamBias, blend));
            oceanMaterial.SetFloat(FoamPowerID, Mathf.Lerp(calmPreset.foamPower, tempestPreset.foamPower, blend));
        }

        private void OnDisable()
        {
            if (Application.isPlaying && GlobalWeatherManager.Instance != null)
            {
                GlobalWeatherManager.Instance.OnWeatherBlendChanged -= ApplyWeatherBlend;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;

            // Invalidate the cache so tweaking a preset's color/wind forces an update
            // even if the blend slider itself hasn't moved.
            _currentBlend = -1f;

            // Unity throws warnings if you modify material properties or GetComponent directly inside OnValidate.
            // Using delayCall defers the update to the next editor frame, making it perfectly safe.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                
                if (fftBinder == null) fftBinder = GetComponent<OceanFFTBinder>();
                
                ApplyWeatherBlend(editorPreviewBlend);
                
                // Force the Scene View to redraw so the artist sees changes instantly
                UnityEditor.SceneView.RepaintAll();
            };
        }
#endif
    }
}