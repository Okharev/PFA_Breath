using TechArtPlayground.Wind;
using UnityEngine;
using System;

namespace TechArtPlayground.Water
{
    [ExecuteAlways]
    public class OceanFFTBinder : MonoBehaviour
    {
        // Property IDs
        private static readonly int DispTexID = Shader.PropertyToID("_DispTex");
        private static readonly int DerivTexID = Shader.PropertyToID("_DerivTex");
        private static readonly int TimeID = Shader.PropertyToID("_Time");
        private static readonly int ResolutionID = Shader.PropertyToID("_Resolution");
        private static readonly int SizeID = Shader.PropertyToID("_Size");
        private static readonly int StepID = Shader.PropertyToID("_Step");
        private static readonly int InputBufferID = Shader.PropertyToID("InputBuffer");
        private static readonly int OutputBufferID = Shader.PropertyToID("OutputBuffer");
        private static readonly int OutputBufferZID = Shader.PropertyToID("OutputBufferZ");
        private static readonly int InputBufferZID = Shader.PropertyToID("InputBufferZ");
        private static readonly int FFTScaleID = Shader.PropertyToID("_FFTScale");
        private static readonly int ChoppinessID = Shader.PropertyToID("_Choppiness");
        private static readonly int WindDirection1ID = Shader.PropertyToID("_WindDirection1");
        private static readonly int NumStagesID = Shader.PropertyToID("_NumStages");
        private static readonly int WindSpeedID = Shader.PropertyToID("_WindSpeed");
        private static readonly int WindDirID = Shader.PropertyToID("_WindDir");
        private static readonly int PhillipsAID = Shader.PropertyToID("_PhillipsA");
        private static readonly int MaxWaveHeightID = Shader.PropertyToID("_MaxWaveHeight");

        [Header("Simulation References")]
        public ComputeShader fftCompute;
        public Material oceanMaterial;

        // ----------------------------------------------------
        // 1. INSPECTOR & ENCAPSULATED PROPERTIES
        // ----------------------------------------------------
        [Header("FFT Settings")]
        [Range(64, 512)] public int resolution = 256; 
        public float timeScale = 1.0f;
        
        [SerializeField] private float oceanSize = 250.0f;
        public float OceanSize 
        { 
            get => oceanSize; 
            set { if (Mathf.Approximately(oceanSize, value)) return; oceanSize = value; PushOceanSize(); UpdateCompositeWaveData(); } 
        }

        [Header("Wave Parameters")]
        [SerializeField] private float windSpeed = 15.0f;
        public float WindSpeed 
        { 
            get => windSpeed; 
            set { if (Mathf.Approximately(windSpeed, value)) return; windSpeed = value; UpdateCompositeWaveData(); } 
        }

        [SerializeField] private Vector2 windDirection = new Vector2(1.0f, 1.0f);
        public Vector2 WindDirection 
        { 
            get => windDirection; 
            set { if (windDirection == value) return; windDirection = value; UpdateCompositeWaveData(); } 
        }

        [SerializeField] private float phillipsAmplitude = 0.005f;
        public float PhillipsAmplitude 
        { 
            get => phillipsAmplitude; 
            set { if (Mathf.Approximately(phillipsAmplitude, value)) return; phillipsAmplitude = value; UpdateCompositeWaveData(); } 
        }

        [Range(0f, 2f)] [SerializeField] private float choppiness = 1.2f;
        public float Choppiness 
        { 
            get => choppiness; 
            set { if (Mathf.Approximately(choppiness, value)) return; choppiness = value; PushChoppiness(); } 
        }

        [Header("Output Textures")]
        public RenderTexture displacementMap;
        public RenderTexture derivativeMap;

        // Working Buffers & Kernels
        private RenderTexture pingBuffer, pongBuffer, pingBufferZ, pongBufferZ;
        private int initKernel, horizontalKernel, verticalKernel, packKernel;
        private int _threadsX, _threadsHalf, _numStages;

        private void OnEnable()
        {
            InitializeTextures();
            CacheKernels();
            CalculateDispatchConstants();
            BindStaticTextures();
            PushAllComputeData();
        }

        private void InitializeTextures()
        {
            displacementMap = CreateRT(resolution, RenderTextureFormat.ARGBFloat, false);
            derivativeMap = CreateRT(resolution, RenderTextureFormat.ARGBHalf, true);
            pingBuffer = CreateRT(resolution, RenderTextureFormat.ARGBFloat, false);
            pongBuffer = CreateRT(resolution, RenderTextureFormat.ARGBFloat, false);
            pingBufferZ = CreateRT(resolution, RenderTextureFormat.RGFloat, false);
            pongBufferZ = CreateRT(resolution, RenderTextureFormat.RGFloat, false);
        }

        private void CalculateDispatchConstants()
        {
            _threadsX = resolution / 8; 
            _threadsHalf = (resolution / 2) / 8; 
            _numStages = (int)Mathf.Log(resolution, 2);

            if (fftCompute != null)
            {
                fftCompute.SetInt(ResolutionID, resolution);
                fftCompute.SetInt(NumStagesID, _numStages); 
            }
        }

        private void BindStaticTextures()
        {
            if (oceanMaterial == null) return;
            oceanMaterial.SetTexture(DispTexID, displacementMap);
            oceanMaterial.SetTexture(DerivTexID, derivativeMap);
        }

        // --- ENCAPSULATED GPU DATA PUSH METHODS ---
        private void PushOceanSize()
        {
            if (fftCompute == null || oceanMaterial == null) return;
            fftCompute.SetFloat(SizeID, oceanSize);
            oceanMaterial.SetFloat(FFTScaleID, 1.0f / Mathf.Max(0.001f, oceanSize));
        }

        private void PushChoppiness()
        {
            if (fftCompute == null || oceanMaterial == null) return;
            fftCompute.SetFloat(ChoppinessID, choppiness);
            oceanMaterial.SetFloat(ChoppinessID, choppiness);
        }

        private void UpdateCompositeWaveData()
        {
            if (fftCompute == null || oceanMaterial == null) return;

            Vector2 normalizedDir = windDirection.normalized;
            fftCompute.SetVector(WindDirID, normalizedDir);
            fftCompute.SetFloat(WindSpeedID, windSpeed);
            
            float normalizedPhillips = phillipsAmplitude * Mathf.Pow(resolution, 4);
            fftCompute.SetFloat(PhillipsAID, normalizedPhillips);

            float estimatedMaxHeight = ((windSpeed * windSpeed) / 9.81f) * phillipsAmplitude * oceanSize; 
            oceanMaterial.SetFloat(MaxWaveHeightID, Mathf.Max(0.5f, estimatedMaxHeight));
            oceanMaterial.SetVector(WindDirection1ID, normalizedDir * (windSpeed * 0.05f));
        }

        private void PushAllComputeData()
        {
            PushOceanSize();
            PushChoppiness();
            UpdateCompositeWaveData();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Removed the play-mode lock. 
            // Wrap in delayCall to safely pass data to ComputeShaders and Materials in edit mode.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !this.isActiveAndEnabled) return;
                PushAllComputeData();
                UnityEditor.SceneView.RepaintAll();
            };
        }
#endif
        
        // Public API for external controllers (Now map directly to properties)
        public void SetWindSpeed(float speed) => WindSpeed = speed;
        public void SetPhillipsAmplitude(float amplitude) => PhillipsAmplitude = amplitude;
        public void SetChoppiness(float chop) => Choppiness = chop;

        private void Update()
        {
            // REMOVED: GlobalWeatherManager polling. 
            // The system is now 100% event-driven through OceanWeatherController!
            
            // This MUST remain to animate the water over time.
            DispatchFFT(); 
        }
        
#if UNITY_EDITOR
        // Expose a safe method to force a compute shader pass in Edit Mode
        // whenever the OceanWeatherController's OnValidate fires.
        public void EditorForceUpdate()
        {
            DispatchFFT();
        }
#endif

        private void DispatchFFT()
        {
            fftCompute.SetFloat(TimeID, Time.unscaledTime * timeScale);
    
            // 1. Initialization
            fftCompute.SetTexture(initKernel, OutputBufferID, pingBuffer);
            fftCompute.SetTexture(initKernel, OutputBufferZID, pingBufferZ); 
            fftCompute.Dispatch(initKernel, _threadsX, _threadsX, 1);

            // 2. Horizontal FFT Passes
            bool pingPong = true; 
            for (int i = 0; i < _numStages; i++)
            {
                fftCompute.SetInt(StepID, i);
                fftCompute.SetTexture(horizontalKernel, InputBufferID, pingPong ? pingBuffer : pongBuffer);
                fftCompute.SetTexture(horizontalKernel, OutputBufferID, pingPong ? pongBuffer : pingBuffer);
                fftCompute.SetTexture(horizontalKernel, InputBufferZID, pingPong ? pingBufferZ : pongBufferZ);
                fftCompute.SetTexture(horizontalKernel, OutputBufferZID, pingPong ? pongBufferZ : pingBufferZ);
                fftCompute.Dispatch(horizontalKernel, _threadsHalf, _threadsX, 1);
                pingPong = !pingPong;
            }

            // 3. Vertical FFT Passes
            for (int i = 0; i < _numStages; i++)
            {
                fftCompute.SetInt(StepID, i);
                fftCompute.SetTexture(verticalKernel, InputBufferID, pingPong ? pingBuffer : pongBuffer);
                fftCompute.SetTexture(verticalKernel, OutputBufferID, pingPong ? pongBuffer : pingBuffer);
                fftCompute.SetTexture(verticalKernel, InputBufferZID, pingPong ? pingBufferZ : pongBufferZ);
                fftCompute.SetTexture(verticalKernel, OutputBufferZID, pingPong ? pongBufferZ : pingBufferZ);
                fftCompute.Dispatch(verticalKernel, _threadsX, _threadsHalf, 1);
                pingPong = !pingPong;
            }

            // 4. Pack into final textures
            RenderTexture finalFFTData = pingPong ? pingBuffer : pongBuffer;
            RenderTexture finalFFTDataZ = pingPong ? pingBufferZ : pongBufferZ;
        
            fftCompute.SetTexture(packKernel, InputBufferID, finalFFTData);
            fftCompute.SetTexture(packKernel, InputBufferZID, finalFFTDataZ);
            fftCompute.SetTexture(packKernel, DispTexID, displacementMap);
            fftCompute.SetTexture(packKernel, DerivTexID, derivativeMap);
            fftCompute.Dispatch(packKernel, _threadsX, _threadsX, 1);

            derivativeMap.GenerateMips();
        }

        private void OnDisable()
        {
            if (displacementMap != null) displacementMap.Release();
            if (derivativeMap != null) derivativeMap.Release();
            if (pingBuffer != null) pingBuffer.Release();
            if (pongBuffer != null) pongBuffer.Release();
            if (pingBufferZ != null) pingBufferZ.Release();
            if (pongBufferZ != null) pongBufferZ.Release();
        }

        private static RenderTexture CreateRT(int size, RenderTextureFormat format, bool useMips)
        {
            RenderTexture rt = new RenderTexture(size, size, 0, format)
            {
                enableRandomWrite = true,
                useMipMap = useMips,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Repeat
            };
            rt.Create();
            return rt;
        }

        private void CacheKernels()
        {
            initKernel = fftCompute.FindKernel("CalculateSpectrum");
            horizontalKernel = fftCompute.FindKernel("FFTHorizontal");
            verticalKernel = fftCompute.FindKernel("FFTVertical");
            packKernel = fftCompute.FindKernel("PackFFTData");
        }
    }
}