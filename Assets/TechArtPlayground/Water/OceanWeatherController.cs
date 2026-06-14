using TechArtPlayground.Wind;
using UnityEngine;

namespace TechArtPlayground.Water
{
    [System.Serializable]
    public struct OceanWeatherPreset
    {
        [Header("Wind & Wave Dynamics")]
        public float windSpeed;
        public float phillipsAmplitude;
        [Range(0f, 2f)] public float choppiness;

        [Header("Color & Depth")]
        [ColorUsage(false, true)] public Color shallowColor;
        [ColorUsage(false, true)] public Color deepColor;
        [Range(0.01f, 2.0f)] public float depthAbsorption;

        [Header("Subsurface Scattering (SSS)")]
        [ColorUsage(true, true)] public Color sssColor;
        [Range(0f, 5f)] public float sssStrength;
        [Range(1f, 20f)] public float sssPower;

        [Header("Foam Settings")]
        [ColorUsage(true, true)] public Color foamColor;
        [Range(-1f, 1f)] public float foamBias;
        [Range(0.1f, 5f)] public float foamPower;
        [Range(0.01f, 1f)] public float shorelineFoamCutoff;

        [Header("Caustics")]
        [Range(0f, 2f)] public float causticsStrength;
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

        // Shader Property IDs
        private static readonly int ShallowColorID = Shader.PropertyToID("_ShallowColor");
        private static readonly int DeepColorID = Shader.PropertyToID("_DeepColor");
        private static readonly int DepthAbsorptionID = Shader.PropertyToID("_DepthAbsorption");
        
        private static readonly int SSSColorID = Shader.PropertyToID("_SSSColor");
        private static readonly int SSSStrengthID = Shader.PropertyToID("_SSSStrength");
        private static readonly int SSSPowerID = Shader.PropertyToID("_SSSPower");

        private static readonly int FoamColorID = Shader.PropertyToID("_FoamColor");
        private static readonly int FoamBiasID = Shader.PropertyToID("_FoamBias");
        private static readonly int FoamPowerID = Shader.PropertyToID("_FoamPower");
        private static readonly int FoamCutoffID = Shader.PropertyToID("_FoamCutoff");

        private static readonly int CausticsStrengthID = Shader.PropertyToID("_CausticsStrength");

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
                ApplyWeatherBlend(Application.isPlaying ? 0f : editorPreviewBlend);
            }
        }

        private void ApplyWeatherBlend(float blend)
        {
            if (fftBinder == null || oceanMaterial == null) return;
            
            // In Play mode, prevent redundant pushes. In Edit mode, allow forced updates.
            if (Application.isPlaying && Mathf.Approximately(_currentBlend, blend)) return;
            _currentBlend = blend;

            // 1. Update Compute Shader (FFT Physics)
            fftBinder.SetWindSpeed(Mathf.Lerp(calmPreset.windSpeed, tempestPreset.windSpeed, blend));
            fftBinder.SetPhillipsAmplitude(Mathf.Lerp(calmPreset.phillipsAmplitude, tempestPreset.phillipsAmplitude, blend));
            fftBinder.SetChoppiness(Mathf.Lerp(calmPreset.choppiness, tempestPreset.choppiness, blend));

            // 2. Update Material (Volumetrics & Color)
            oceanMaterial.SetColor(ShallowColorID, Color.Lerp(calmPreset.shallowColor, tempestPreset.shallowColor, blend));
            oceanMaterial.SetColor(DeepColorID, Color.Lerp(calmPreset.deepColor, tempestPreset.deepColor, blend));
            oceanMaterial.SetFloat(DepthAbsorptionID, Mathf.Lerp(calmPreset.depthAbsorption, tempestPreset.depthAbsorption, blend));

            oceanMaterial.SetColor(SSSColorID, Color.Lerp(calmPreset.sssColor, tempestPreset.sssColor, blend));
            oceanMaterial.SetFloat(SSSStrengthID, Mathf.Lerp(calmPreset.sssStrength, tempestPreset.sssStrength, blend));
            oceanMaterial.SetFloat(SSSPowerID, Mathf.Lerp(calmPreset.sssPower, tempestPreset.sssPower, blend));

            oceanMaterial.SetColor(FoamColorID, Color.Lerp(calmPreset.foamColor, tempestPreset.foamColor, blend));
            oceanMaterial.SetFloat(FoamBiasID, Mathf.Lerp(calmPreset.foamBias, tempestPreset.foamBias, blend));
            oceanMaterial.SetFloat(FoamPowerID, Mathf.Lerp(calmPreset.foamPower, tempestPreset.foamPower, blend));
            oceanMaterial.SetFloat(FoamCutoffID, Mathf.Lerp(calmPreset.shorelineFoamCutoff, tempestPreset.shorelineFoamCutoff, blend));

            oceanMaterial.SetFloat(CausticsStrengthID, Mathf.Lerp(calmPreset.causticsStrength, tempestPreset.causticsStrength, blend));
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

            _currentBlend = -1f; // Invalidate cache

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (fftBinder == null) fftBinder = GetComponent<OceanFFTBinder>();
                
                ApplyWeatherBlend(editorPreviewBlend);

                // CRITICAL FIX: Force the FFT compute shader to dispatch one frame 
                // so the displacement textures physically update in the Editor view.
                if (fftBinder != null && fftBinder.isActiveAndEnabled)
                {
                    fftBinder.EditorForceUpdate();
                }
                
                UnityEditor.SceneView.RepaintAll();
            };
        }
#endif
    }
}