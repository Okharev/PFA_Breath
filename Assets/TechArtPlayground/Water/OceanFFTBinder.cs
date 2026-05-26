using UnityEngine;

namespace TechArtPlayground.Water
{
    [ExecuteAlways]
    public class OceanFFTBinder : MonoBehaviour
    {
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
        private static readonly int Choppiness = Shader.PropertyToID("_Choppiness");
        private static readonly int WindDirection1 = Shader.PropertyToID("_WindDirection1");
        private static readonly int NumStages = Shader.PropertyToID("_NumStages");
        private static readonly int WindSpeed = Shader.PropertyToID("_WindSpeed");
        private static readonly int WindDir = Shader.PropertyToID("_WindDir");
        private static readonly int PhillipsA = Shader.PropertyToID("_PhillipsA");

        [Header("Simulation References")]
        public ComputeShader fftCompute;
        public Material oceanMaterial;

        [Header("FFT Settings")]
        [Range(64, 512)] public int resolution = 256; 
        public float timeScale = 1.0f;
        public float oceanSize = 250.0f;

        [Header("Wave Parameters")]
        public float windSpeed = 15.0f;
        public Vector2 windDirection = new Vector2(1.0f, 1.0f);
        public float phillipsAmplitude = 0.005f;
        [Range(0f, 2f)] public float choppiness = 1.2f;

        [Header("Output Textures")]
        public RenderTexture displacementMap;
        public RenderTexture derivativeMap;

        // Ping-Pong Buffers for Height & X-Displacement (float4)
        private RenderTexture pingBuffer;
        private RenderTexture pongBuffer;
    
        // Ping-Pong Buffers for Z-Displacement (float2)
        private RenderTexture pingBufferZ;
        private RenderTexture pongBufferZ;

        // Kernel IDs
        private int initKernel;
        private int horizontalKernel;
        private int verticalKernel;
        private int packKernel;

        void Start()
        {
            InitializeTextures();
            CacheKernels();
        }

        void InitializeTextures()
        {
            // Output Maps for the Material
            displacementMap = CreateRT(resolution, RenderTextureFormat.ARGBFloat, false);
            derivativeMap = CreateRT(resolution, RenderTextureFormat.ARGBHalf, true); // Mips required for foam
        
            // Working buffers for Height & X (Float4)
            pingBuffer = CreateRT(resolution, RenderTextureFormat.ARGBFloat, false);
            pongBuffer = CreateRT(resolution, RenderTextureFormat.ARGBFloat, false);
        
            // Working buffers for Z (Float2/RGFloat)
            pingBufferZ = CreateRT(resolution, RenderTextureFormat.RGFloat, false);
            pongBufferZ = CreateRT(resolution, RenderTextureFormat.RGFloat, false);
        }

        RenderTexture CreateRT(int size, RenderTextureFormat format, bool useMips)
        {
            RenderTexture rt = new RenderTexture(size, size, 0, format);
            rt.enableRandomWrite = true;
            rt.useMipMap = useMips;
            rt.autoGenerateMips = false;
    
            // CRITICAL FOR FFT OCEANS: Ensure waves tile seamlessly across bounds
            rt.wrapMode = TextureWrapMode.Repeat; 
    
            rt.Create();
            return rt;
        }
        void CacheKernels()
        {
            initKernel = fftCompute.FindKernel("CalculateSpectrum");
            horizontalKernel = fftCompute.FindKernel("FFTHorizontal");
            verticalKernel = fftCompute.FindKernel("FFTVertical");
            packKernel = fftCompute.FindKernel("PackFFTData");
        }

        void Update()
        {

            DispatchFFT();

            // 1. Bind the simulation textures
            oceanMaterial.SetTexture(DispTex, displacementMap);
            oceanMaterial.SetTexture(DerivTex, derivativeMap);

            // =========================================================
            // AUTOMATED MATERIAL SETTINGS
            // =========================================================
        
            // 2. Auto-calculate the UV Scale (1.0 / Domain Size)
            oceanMaterial.SetFloat(FFTScale, 1.0f / oceanSize);
        
            // 3. Sync Choppiness so the shader and compute always match
            oceanMaterial.SetFloat(Choppiness, choppiness);
        
            // 4. (Optional) Sync the material's micro-normal wind to match the FFT wind
            oceanMaterial.SetVector(WindDirection1, windDirection.normalized * (windSpeed * 0.05f));
        }

        private void DispatchFFT()
        {
            int threadsX = resolution / 8; 
            int threadsHalf = (resolution / 2) / 8; 
            int numStages = (int)Mathf.Log(resolution, 2);

            // Compensate for the IFFT Normalizer (1 / N^2) dynamically.
            // Since h0 = sqrt(Phillips), we multiply Phillips by N^4 to scale h0 by N^2.
            float normalizedPhillips = phillipsAmplitude * Mathf.Pow(resolution, 4);

            // --- 0. Set Global Simulation Parameters ---
            fftCompute.SetFloat(Time1, Time.time * timeScale);
            fftCompute.SetInt(Resolution1, resolution);
            fftCompute.SetInt(NumStages, numStages); 
            fftCompute.SetFloat(Size, oceanSize);
            fftCompute.SetFloat(WindSpeed, windSpeed);
            fftCompute.SetVector(WindDir, windDirection.normalized);
    
            // Pass the massively boosted amplitude here
            fftCompute.SetFloat(PhillipsA, normalizedPhillips); 
    
            fftCompute.SetFloat(Choppiness, choppiness);

            // --- 1. Initialization (Spectrum Generation) ---
            fftCompute.SetTexture(initKernel, OutputBuffer, pingBuffer);
            fftCompute.SetTexture(initKernel, OutputBufferZ, pingBufferZ); 
            fftCompute.Dispatch(initKernel, threadsX, threadsX, 1);

            // --- 2. Horizontal FFT Passes ---
            bool pingPong = true; 
            for (int i = 0; i < numStages; i++)
            {
                fftCompute.SetInt(Step, i);
            
                fftCompute.SetTexture(horizontalKernel, InputBuffer, pingPong ? pingBuffer : pongBuffer);
                fftCompute.SetTexture(horizontalKernel, OutputBuffer, pingPong ? pongBuffer : pingBuffer);
                fftCompute.SetTexture(horizontalKernel, InputBufferZ, pingPong ? pingBufferZ : pongBufferZ);
                fftCompute.SetTexture(horizontalKernel, OutputBufferZ, pingPong ? pongBufferZ : pingBufferZ);
            
                // FIX 2: Dispatch N/2 threads horizontally, N threads vertically
                fftCompute.Dispatch(horizontalKernel, threadsHalf, threadsX, 1);
                pingPong = !pingPong;
            }

            // --- 3. Vertical FFT Passes ---
            for (int i = 0; i < numStages; i++)
            {
                fftCompute.SetInt(Step, i);
            
                fftCompute.SetTexture(verticalKernel, InputBuffer, pingPong ? pingBuffer : pongBuffer);
                fftCompute.SetTexture(verticalKernel, OutputBuffer, pingPong ? pongBuffer : pingBuffer);
                fftCompute.SetTexture(verticalKernel, InputBufferZ, pingPong ? pingBufferZ : pongBufferZ);
                fftCompute.SetTexture(verticalKernel, OutputBufferZ, pingPong ? pongBufferZ : pingBufferZ);
            
                // FIX 3: Dispatch N threads horizontally, N/2 threads vertically
                fftCompute.Dispatch(verticalKernel, threadsX, threadsHalf, 1);
                pingPong = !pingPong;
            }

            // --- 4. Pack into final material textures ---
            RenderTexture finalFFTData = pingPong ? pingBuffer : pongBuffer;
            RenderTexture finalFFTDataZ = pingPong ? pingBufferZ : pongBufferZ;
        
            fftCompute.SetTexture(packKernel, InputBuffer, finalFFTData);
            fftCompute.SetTexture(packKernel, InputBufferZ, finalFFTDataZ);
            fftCompute.SetTexture(packKernel, DispTex, displacementMap);
            fftCompute.SetTexture(packKernel, DerivTex, derivativeMap);
        
            // Pack runs over the full N x N grid
            fftCompute.Dispatch(packKernel, threadsX, threadsX, 1);

            // --- 5. Generate MipMaps ---
            derivativeMap.GenerateMips();
        }

        void OnDestroy()
        {
            if (displacementMap != null) displacementMap.Release();
            if (derivativeMap != null) derivativeMap.Release();
            if (pingBuffer != null) pingBuffer.Release();
            if (pongBuffer != null) pongBuffer.Release();
            if (pingBufferZ != null) pingBufferZ.Release();
            if (pongBufferZ != null) pongBufferZ.Release();
        }
    }
}