using TechArtPlayground.Wind;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TechArtPlayground.Rain
{
    // STRICT ALIGNMENT: 48 bytes total
    // Types: 0.0 = Floor Splash, 1.0 = Wall Shatter, 2.0 = Edge Drip
    public struct Splash
    {
        public Vector4 posAndLife;       // xyz = position, w = life
        public Vector4 normalAndMaxLife; // xyz = normal, w = maxLife
        public Vector4 typeAndPadding;   // x = type, yzw = padding
    }
    
    public class RainManager : MonoBehaviour
    {
        private static readonly int RainBufferId = Shader.PropertyToID("_RainBuffer");
        private static readonly int GridSizeId = Shader.PropertyToID("_GridSize");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int TimeId = Shader.PropertyToID("_Time");
        private static readonly int CameraPosId = Shader.PropertyToID("_CameraPos");
        private static readonly int RainVelocityId = Shader.PropertyToID("_RainVelocity");
        private static readonly int RainColorId = Shader.PropertyToID("_RainColor");
        private static readonly int OcclusionMapId = Shader.PropertyToID("_OcclusionMap");
        private static readonly int OcclusionCenterId = Shader.PropertyToID("_OcclusionCenter");
        private static readonly int OcclusionOrthoSizeId = Shader.PropertyToID("_OcclusionOrthoSize");
        private static readonly int OcclusionCameraYId = Shader.PropertyToID("_OcclusionCameraY");
        private static readonly int IsReversedZId = Shader.PropertyToID("_IsReversedZ");
        private static readonly int FarClipPlaneId = Shader.PropertyToID("_FarClipPlane");
        private static readonly int RequestCount = Shader.PropertyToID("_RequestCount");

        private struct RainDrop
        {
            public Vector3 position;
            public float randomSeed;
        }

        [Header("Dependencies")]
        public ComputeShader rainCompute;
        public Material rainMaterial;
        public Camera mainCamera;

        [Header("Occlusion Settings")]
        public LayerMask rainOcclusionLayer;
        public float occlusionCameraHeight = 150f;
        
        [Header("Weather Settings")]
        public int maxParticleCount = 50000;
        public float maxFallSpeed = 35f;
        public float minFallSpeed = 10f;
        [Range(0f, 1f)] public float cameraVelocityCompensation = 0.5f;

        [Header("System State (Read Only)")]
        [SerializeField] private int currentActiveParticles;
        [SerializeField] private float _gridSize = 40f;
        
        [Header("Splash Settings")]
        public ComputeShader splashCompute;
        public Material splashMaterial;
        public int maxSplashes = 20000;

        private GraphicsBuffer splashRequestsBuffer;
        private GraphicsBuffer splashPoolBuffer;
        private GraphicsBuffer splashArgsBuffer;
        private GraphicsBuffer poolIndexBuffer;
        private GraphicsBuffer copyCountBuffer;

        private Mesh quadMesh;
        private RenderParams splashRenderParams;

        private int splashProcessKernel;
        private int splashUpdateKernel;

        private GraphicsBuffer rainBuffer;
        private GraphicsBuffer argsBuffer;
        private Mesh crossMesh;
        private RenderParams renderParams;

        private int kernelID;
        private int threadGroups;
        private Color baseRainColor;
        
        private Vector3 lastCameraPos;
        private Vector3 smoothedCameraVelocity;

        private Camera occlusionCamera;
        private RenderTexture occlusionTexture;
        private const float OCCLUSION_FAR_CLIP = 300f; 

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            baseRainColor = rainMaterial.GetColor(RainColorId);
            lastCameraPos = mainCamera.transform.position;

            kernelID = rainCompute.FindKernel("UpdateRain");
            splashProcessKernel = splashCompute.FindKernel("ProcessSplashRequests");
            splashUpdateKernel = splashCompute.FindKernel("UpdateSplashes");

            splashRequestsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, maxParticleCount, 32); 
            splashRequestsBuffer.SetCounterValue(0);

            splashPoolBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxSplashes, 48); 
            splashPoolBuffer.SetData(new Splash[maxSplashes]);

            poolIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 4);
            poolIndexBuffer.SetData(new uint[] { 0 });

            copyCountBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 4);

            rainCompute.SetBuffer(kernelID, "_SplashRequests", splashRequestsBuffer);

            splashCompute.SetBuffer(splashProcessKernel, "_SplashRequests", splashRequestsBuffer);
            splashCompute.SetBuffer(splashProcessKernel, "_SplashPool", splashPoolBuffer);
            splashCompute.SetBuffer(splashProcessKernel, "_PoolIndex", poolIndexBuffer);
            splashCompute.SetInt("_MaxSplashes", maxSplashes);

            splashCompute.SetBuffer(splashUpdateKernel, "_SplashPool", splashPoolBuffer);
            splashCompute.SetInt("_MaxSplashes", maxSplashes);

            splashMaterial.SetBuffer("_SplashPool", splashPoolBuffer);
            splashRenderParams = new RenderParams(splashMaterial) { worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f) };
            CreateSplashQuad(); 

            SetupOcclusionCamera();
            InitializeBuffers();
            CreateCrossMesh();
        
            renderParams = new RenderParams(rainMaterial)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f),
                matProps = new MaterialPropertyBlock()
            };

            rainCompute.SetFloat(GridSizeId, _gridSize);
            rainMaterial.SetFloat(GridSizeId, _gridSize);

            if (GlobalWeatherManager.Instance != null)
            {
                GlobalWeatherManager.Instance.OnWeatherBlendChanged += OnWeatherChanged;
                OnWeatherChanged(GlobalWeatherManager.Instance.currentBlend);
            }
        }

        private void SetupOcclusionCamera()
        {
            occlusionTexture = new RenderTexture(512*2, 512*2, 16, RenderTextureFormat.Depth);
            occlusionTexture.name = "RainOcclusionDepthMap";
            occlusionTexture.filterMode = FilterMode.Point; 
            occlusionTexture.wrapMode = TextureWrapMode.Clamp; 
            occlusionTexture.Create();

            GameObject camObj = new GameObject("RainOcclusionCamera");
            camObj.transform.SetParent(transform); 
            
            occlusionCamera = camObj.AddComponent<Camera>();
            occlusionCamera.cameraType = CameraType.Game; 
            occlusionCamera.orthographic = true;
            occlusionCamera.orthographicSize = _gridSize * 0.5f; 
            occlusionCamera.nearClipPlane = 0.01f;
            occlusionCamera.farClipPlane = OCCLUSION_FAR_CLIP;
            occlusionCamera.cullingMask = rainOcclusionLayer;
            
            occlusionCamera.clearFlags = CameraClearFlags.SolidColor;
            occlusionCamera.backgroundColor = Color.black; 
            
            occlusionCamera.targetTexture = occlusionTexture;
            occlusionCamera.enabled = true;

            var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderShadows = false;
            camData.requiresColorOption = CameraOverrideOption.Off;
            camData.requiresDepthOption = CameraOverrideOption.On;

            rainCompute.SetTexture(kernelID, OcclusionMapId, occlusionTexture);
            rainCompute.SetFloat(OcclusionOrthoSizeId, _gridSize * 0.5f);
            rainCompute.SetFloat(FarClipPlaneId, OCCLUSION_FAR_CLIP);
            rainCompute.SetInt(IsReversedZId, SystemInfo.usesReversedZBuffer ? 1 : 0);
        }

        private void OnWeatherChanged(float blend)
        {
            currentActiveParticles = Mathf.FloorToInt(maxParticleCount * blend);

            if (argsBuffer != null && crossMesh != null)
            {
                var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
                args[0].indexCountPerInstance = crossMesh.GetIndexCount(0);
                args[0].instanceCount = (uint)currentActiveParticles;
                argsBuffer.SetData(args);
            }

            threadGroups = Mathf.CeilToInt(currentActiveParticles / 128f);

            Color adjustedColor = baseRainColor;
            adjustedColor.a *= Mathf.Clamp01(blend * 4f); 
            rainMaterial.SetColor(RainColorId, adjustedColor);
        }

        private void InitializeBuffers()
        {
            rainBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxParticleCount, 16);
            RainDrop[] drops = new RainDrop[maxParticleCount];

            float halfGrid = _gridSize * 0.5f;

            for (int i = 0; i < maxParticleCount; i++)
            {
                drops[i] = new RainDrop
                {
                    position = new Vector3(
                        Random.Range(-halfGrid, halfGrid),
                        Random.Range(-halfGrid, halfGrid),
                        Random.Range(-halfGrid, halfGrid)
                    ),
                    randomSeed = Random.Range(0.8f, 1.2f)
                };
            }
            rainBuffer.SetData(drops);
            rainCompute.SetBuffer(kernelID, RainBufferId, rainBuffer);
            rainMaterial.SetBuffer(RainBufferId, rainBuffer);
            argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        }

        private void CreateCrossMesh()
        {
            crossMesh = new Mesh { name = "RainCross" };
            Vector3[] vertices = new Vector3[8];
            Vector2[] uvs = new Vector2[8];
            int[] indices = new int[12];

            vertices[0] = new Vector3(-0.5f, -0.5f, 0); uvs[0] = new Vector2(0, 0);
            vertices[1] = new Vector3(-0.5f,  0.5f, 0); uvs[1] = new Vector2(0, 1);
            vertices[2] = new Vector3( 0.5f,  0.5f, 0); uvs[2] = new Vector2(1, 1);
            vertices[3] = new Vector3( 0.5f, -0.5f, 0); uvs[3] = new Vector2(1, 0);
            indices[0] = 0; indices[1] = 1; indices[2] = 2; indices[3] = 0; indices[4] = 2; indices[5] = 3;

            vertices[4] = new Vector3(0, -0.5f, -0.5f); uvs[4] = new Vector2(0, 0);
            vertices[5] = new Vector3(0,  0.5f, -0.5f); uvs[5] = new Vector2(0, 1);
            vertices[6] = new Vector3(0,  0.5f,  0.5f); uvs[6] = new Vector2(1, 1);
            vertices[7] = new Vector3(0, -0.5f,  0.5f); uvs[7] = new Vector2(1, 0);
            indices[6] = 4; indices[7] = 5; indices[8] = 6; indices[9] = 4; indices[10] = 6; indices[11] = 7;

            crossMesh.vertices = vertices;
            crossMesh.uv = uvs;
            crossMesh.triangles = indices;
        }

        private void Update()
        {
            if (currentActiveParticles <= 0) return;

            Vector3 cameraPos = mainCamera.transform.position;

            Vector3 rawCameraVelocity = (cameraPos - lastCameraPos) / Mathf.Max(Time.deltaTime, 0.0001f);
            smoothedCameraVelocity = Vector3.Lerp(smoothedCameraVelocity, rawCameraVelocity, Time.deltaTime * 10f);
            lastCameraPos = cameraPos;

            Vector3 simulationCenter = cameraPos + (Vector3.up * (_gridSize * 0.1f));

            occlusionCamera.transform.position = new Vector3(simulationCenter.x, simulationCenter.y + occlusionCameraHeight, simulationCenter.z);
            occlusionCamera.transform.rotation = Quaternion.Euler(90, 0, 0);

            rainCompute.SetVector(OcclusionCenterId, occlusionCamera.transform.position);
            rainCompute.SetFloat(OcclusionCameraYId, occlusionCamera.transform.position.y);

            float blend = GlobalWeatherManager.Instance != null ? GlobalWeatherManager.Instance.currentBlend : 1f;
            float currentFallSpeed = Mathf.Lerp(minFallSpeed, maxFallSpeed, blend);
            Vector3 windVel = GlobalWeatherManager.Instance != null ? GlobalWeatherManager.Instance.CurrentWindVelocity : Vector3.zero;
            Vector3 baseRainVelocity = new Vector3(windVel.x, -currentFallSpeed, windVel.z);
            Vector3 finalRainVelocity = baseRainVelocity - (smoothedCameraVelocity * cameraVelocityCompensation);

            rainCompute.SetFloat(DeltaTimeId, Time.deltaTime);
            rainCompute.SetFloat(TimeId, Time.time);
            rainCompute.SetVector(RainVelocityId, finalRainVelocity);
            rainMaterial.SetVector(RainVelocityId, finalRainVelocity);
            rainCompute.SetVector(CameraPosId, simulationCenter);

            rainCompute.Dispatch(kernelID, threadGroups, 1, 1);
            
            GraphicsBuffer.CopyCount(splashRequestsBuffer, copyCountBuffer, 0);

            uint[] countData = new uint[1];
            copyCountBuffer.GetData(countData);
            uint requestCount = countData[0];

            if (requestCount > 0)
            {
                splashCompute.SetInt(RequestCount, (int)requestCount);
                int processGroups = Mathf.CeilToInt(requestCount / 64f);
                splashCompute.Dispatch(splashProcessKernel, processGroups, 1, 1);
            }

            splashRequestsBuffer.SetCounterValue(0);

            splashCompute.SetFloat(DeltaTimeId, Time.deltaTime);
            int updateGroups = Mathf.CeilToInt(maxSplashes / 128f);
            splashCompute.Dispatch(splashUpdateKernel, updateGroups, 1, 1);

            Graphics.RenderMeshIndirect(splashRenderParams, quadMesh, splashArgsBuffer, 1);
            Graphics.RenderMeshIndirect(renderParams, crossMesh, argsBuffer, 1);
        }
        
        private void CreateSplashQuad()
        {
            quadMesh = new Mesh { name = "SplashQuad" };
            quadMesh.vertices = new Vector3[] { 
                new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), 
                new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0) 
            };
            quadMesh.uv = new Vector2[] { new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0) };
            quadMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };

            splashArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            args[0].indexCountPerInstance = quadMesh.GetIndexCount(0);
            args[0].instanceCount = (uint)maxSplashes;
            splashArgsBuffer.SetData(args);
        }

        private void OnDestroy()
        {
            if (GlobalWeatherManager.Instance != null)
                GlobalWeatherManager.Instance.OnWeatherBlendChanged -= OnWeatherChanged;

            rainBuffer?.Release();
            argsBuffer?.Release();
            splashRequestsBuffer?.Release();
            splashPoolBuffer?.Release();
            splashArgsBuffer?.Release();
            poolIndexBuffer?.Release();
            copyCountBuffer?.Release();
            
            if (occlusionTexture != null)
            {
                occlusionTexture.Release();
                Destroy(occlusionTexture);
            }
        }
    }
}