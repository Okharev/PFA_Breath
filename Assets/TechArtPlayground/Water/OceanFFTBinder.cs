using TechArtPlayground.Wind;
using UnityEngine;
using R3;
using System;

namespace TechArtPlayground.Water
{
    [ExecuteAlways]
    public class OceanFFTBinder : MonoBehaviour
    {
        // Property IDs
        private static readonly int DispTex = Shader.PropertyToID("_DispTex");
        private static readonly int DerivTex = Shader.PropertyToID("_DerivTex");
        private static readonly int Time1 = Shader.PropertyToID("_Time");
        private static readonly int Resolution1 = Shader.PropertyToID("_Resolution");
        private static readonly int Size = Shader.PropertyToID("_Size");
        private static readonly int Step = Shader.PropertyToID("_Step");
        private static readonly int InputBuffer = Shader.PropertyToID("InputBuffer");
        private static readonly int OutputBuffer = Shader.PropertyToID("OutputBuffer");
        private static readonly int OutputBufferZ = Shader.PropertyToID("OutputBufferZ");
        private static readonly int InputBufferZ = Shader.PropertyToID("InputBufferZ");
        private static readonly int FFTScale = Shader.PropertyToID("_FFTScale");
        private static readonly int ChoppinessID = Shader.PropertyToID("_Choppiness");
        private static readonly int WindDirection1 = Shader.PropertyToID("_WindDirection1");
        private static readonly int NumStages = Shader.PropertyToID("_NumStages");
        private static readonly int WindSpeedID = Shader.PropertyToID("_WindSpeed");
        private static readonly int WindDir = Shader.PropertyToID("_WindDir");
        private static readonly int PhillipsA = Shader.PropertyToID("_PhillipsA");
        private static readonly int MaxWaveHeight = Shader.PropertyToID("_MaxWaveHeight");

        [Header("Simulation References")]
        public ComputeShader fftCompute;
        public Material oceanMaterial;

        // ----------------------------------------------------
        // 1. INSPECTOR FRONT-END (Data Storage)
        // ----------------------------------------------------
        [Header("FFT Settings")]
        [Range(64, 512)] public int resolution = 256; 
        public float timeScale = 1.0f;
        [SerializeField] private float oceanSize = 250.0f;

        [Header("Wave Parameters")]
        [SerializeField] private float windSpeed = 15.0f;
        [SerializeField] private Vector2 windDirection = new Vector2(1.0f, 1.0f);
        [SerializeField] private float phillipsAmplitude = 0.005f;
        [Range(0f, 2f)] [SerializeField] private float choppiness = 1.2f;

        [Header("Output Textures")]
        public RenderTexture displacementMap;
        public RenderTexture derivativeMap;

        // ----------------------------------------------------
        // 2. R3 BACK-END (Push-based States)
        // ----------------------------------------------------
        private readonly ReactiveProperty<float> _oceanSizeRx = new();
        private readonly ReactiveProperty<float> _windSpeedRx = new();
        private readonly ReactiveProperty<Vector2> _windDirectionRx = new();
        private readonly ReactiveProperty<float> _phillipsRx = new();
        private readonly ReactiveProperty<float> _choppinessRx = new();
        
        private DisposableBag _disposables;

        // Working Buffers & Kernels
        private RenderTexture pingBuffer, pongBuffer, pingBufferZ, pongBufferZ;
        private int initKernel, horizontalKernel, verticalKernel, packKernel;
        private int _threadsX, _threadsHalf, _numStages;

        private void OnEnable()
        {
            _disposables = new DisposableBag();
            
            InitializeTextures();
            CacheKernels();
            CalculateDispatchConstants();
            BindStaticTextures();
            InitializeReactivePipelines();

            // Set initial inspector values into the reactive streams
            ForceUpdateReactiveState();
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

            // Set truly static compute variables ONCE
            fftCompute.SetInt(Resolution1, resolution);
            fftCompute.SetInt(NumStages, _numStages); 
        }

        private void BindStaticTextures()
        {
            // O(1) Optimization: Bind RTs to the material exactly once.
            // As the Compute Shader manipulates the memory, the Material reads it automatically.
            oceanMaterial.SetTexture(DispTex, displacementMap);
            oceanMaterial.SetTexture(DerivTex, derivativeMap);
        }

        private void InitializeReactivePipelines()
        {
            // Size Pipeline
            _oceanSizeRx.DistinctUntilChanged()
                .Subscribe(this, (size, state) => 
                {
                    state.fftCompute.SetFloat(Size, size);
                    state.oceanMaterial.SetFloat(FFTScale, 1.0f / Mathf.Max(0.001f, size)); // Protect divide by zero
                }).AddTo(ref _disposables);

            // Choppiness Pipeline
            _choppinessRx.DistinctUntilChanged()
                .Subscribe(this, (chop, state) => 
                {
                    state.fftCompute.SetFloat(ChoppinessID, chop);
                    state.oceanMaterial.SetFloat(ChoppinessID, chop);
                }).AddTo(ref _disposables);

            // Wind Direction Pipeline
            _windDirectionRx.DistinctUntilChanged()
                .Subscribe(this, (dir, state) => 
                {
                    Vector2 normalizedDir = dir.normalized;
                    state.fftCompute.SetVector(WindDir, normalizedDir);
                    // Note: _windSpeedRx is accessed via 'state' to avoid closure
                    state.oceanMaterial.SetVector(WindDirection1, normalizedDir * (state._windSpeedRx.Value * 0.05f));
                }).AddTo(ref _disposables);

            // Composite Pipeline: Dependencies for MaxWaveHeight and Phillips
            _windSpeedRx.CombineLatest(_phillipsRx, (speed, phillips) => (speed, phillips))
                .Subscribe(this, (tuple, state) => 
                {
                    state.fftCompute.SetFloat(WindSpeedID, tuple.speed);
                    
                    // Update normalized Phillips using state.resolution
                    float normalizedPhillips = tuple.phillips * Mathf.Pow(state.resolution, 4);
                    state.fftCompute.SetFloat(PhillipsA, normalizedPhillips);

                    // Dynamic SSS Normalization calculation using state._oceanSizeRx
                    const float gravity = 9.81f;
                    float estimatedMaxHeight = ((tuple.speed * tuple.speed) / gravity) * tuple.phillips * state._oceanSizeRx.Value; 
                    state.oceanMaterial.SetFloat(MaxWaveHeight, Mathf.Max(0.5f, estimatedMaxHeight));
                    
                    // Update Material Wind direction scale using state._windDirectionRx
                    state.oceanMaterial.SetVector(WindDirection1, state._windDirectionRx.Value.normalized * (tuple.speed * 0.05f));
                }).AddTo(ref _disposables);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Bridge Unity Inspector dragging to the R3 streams
            if (!Application.isPlaying) return;
            ForceUpdateReactiveState();
        }
#endif

        private void ForceUpdateReactiveState()
        {
            _oceanSizeRx.Value = oceanSize;
            _windSpeedRx.Value = windSpeed;
            _windDirectionRx.Value = windDirection;
            _phillipsRx.Value = phillipsAmplitude;
            _choppinessRx.Value = choppiness;
        }
        
        // Public API for external controllers
        public void SetWindSpeed(float speed) => _windSpeedRx.Value = speed;
        public void SetPhillipsAmplitude(float amplitude) => _phillipsRx.Value = amplitude;
        public void SetChoppiness(float chop) => _choppinessRx.Value = chop;

        private void Update()
        {
            // Poll external weather system, but pipe into R3. 
            // DistinctUntilChanged() acts as a firewall, stopping execution if it hasn't shifted.
            if (GlobalWeatherManager.Instance != null)
            {
                Vector3 globalDir = GlobalWeatherManager.Instance.CurrentWindVelocity;
                Vector2 globalWind2D = new Vector2(globalDir.x, globalDir.z);

                if (globalWind2D.sqrMagnitude > 0.001f)
                    _windDirectionRx.Value = globalWind2D; // Modifies state, R3 handles propagation
            }

            // ONLY execute dynamic, continuous data here
            DispatchFFT();
        }

        private void DispatchFFT()
        {
            // Continuous Time parameter
            fftCompute.SetFloat(Time1, Time.unscaledTime * timeScale);
    
            // --- 1. Initialization (Spectrum Generation) ---
            fftCompute.SetTexture(initKernel, OutputBuffer, pingBuffer);
            fftCompute.SetTexture(initKernel, OutputBufferZ, pingBufferZ); 
            fftCompute.Dispatch(initKernel, _threadsX, _threadsX, 1);

            // --- 2. Horizontal FFT Passes ---
            bool pingPong = true; 
            for (int i = 0; i < _numStages; i++)
            {
                fftCompute.SetInt(Step, i);
                fftCompute.SetTexture(horizontalKernel, InputBuffer, pingPong ? pingBuffer : pongBuffer);
                fftCompute.SetTexture(horizontalKernel, OutputBuffer, pingPong ? pongBuffer : pingBuffer);
                fftCompute.SetTexture(horizontalKernel, InputBufferZ, pingPong ? pingBufferZ : pongBufferZ);
                fftCompute.SetTexture(horizontalKernel, OutputBufferZ, pingPong ? pongBufferZ : pingBufferZ);
                fftCompute.Dispatch(horizontalKernel, _threadsHalf, _threadsX, 1);
                pingPong = !pingPong;
            }

            // --- 3. Vertical FFT Passes ---
            for (int i = 0; i < _numStages; i++)
            {
                fftCompute.SetInt(Step, i);
                fftCompute.SetTexture(verticalKernel, InputBuffer, pingPong ? pingBuffer : pongBuffer);
                fftCompute.SetTexture(verticalKernel, OutputBuffer, pingPong ? pongBuffer : pingBuffer);
                fftCompute.SetTexture(verticalKernel, InputBufferZ, pingPong ? pingBufferZ : pongBufferZ);
                fftCompute.SetTexture(verticalKernel, OutputBufferZ, pingPong ? pongBufferZ : pingBufferZ);
                fftCompute.Dispatch(verticalKernel, _threadsX, _threadsHalf, 1);
                pingPong = !pingPong;
            }

            // --- 4. Pack into final material textures ---
            RenderTexture finalFFTData = pingPong ? pingBuffer : pongBuffer;
            RenderTexture finalFFTDataZ = pingPong ? pingBufferZ : pongBufferZ;
        
            fftCompute.SetTexture(packKernel, InputBuffer, finalFFTData);
            fftCompute.SetTexture(packKernel, InputBufferZ, finalFFTDataZ);
            fftCompute.SetTexture(packKernel, DispTex, displacementMap);
            fftCompute.SetTexture(packKernel, DerivTex, derivativeMap);
            fftCompute.Dispatch(packKernel, _threadsX, _threadsX, 1);

            // --- 5. Generate MipMaps ---
            derivativeMap.GenerateMips();
        }

        private void OnDisable()
        {
            _disposables.Dispose();

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