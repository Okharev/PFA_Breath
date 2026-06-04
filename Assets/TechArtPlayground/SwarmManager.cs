using UnityEngine;
using UnityEngine.Rendering;

namespace TechArtPlayground
{
    public class SwarmManager : MonoBehaviour
    {
        // Continuous properties and Buffer IDs
        private static readonly int ReadBoidsBuffer = Shader.PropertyToID("ReadBoidsBuffer");
        private static readonly int SortBuffer = Shader.PropertyToID("SortBuffer");
        private static readonly int GridOffsets = Shader.PropertyToID("gridOffsets");
        private static readonly int NumBoids = Shader.PropertyToID("numBoids");
        private static readonly int PaddedCount = Shader.PropertyToID("paddedCount");
        private static readonly int WriteBoidsBuffer = Shader.PropertyToID("WriteBoidsBuffer");
        private static readonly int SplineBuffer = Shader.PropertyToID("splineBuffer");
        private static readonly int DeltaTime = Shader.PropertyToID("deltaTime");
        private static readonly int Time1 = Shader.PropertyToID("time");

        private static readonly int SplineResolution = Shader.PropertyToID("splineResolution");
        private static readonly int SplineLength = Shader.PropertyToID("splineLength");
        private static readonly int AttractorsBuffer = Shader.PropertyToID("attractorsBuffer");
        private static readonly int NumAttractors = Shader.PropertyToID("numAttractors");
        private static readonly int PredatorsBuffer = Shader.PropertyToID("predatorsBuffer");
        private static readonly int NumPredators = Shader.PropertyToID("numPredators");
        private static readonly int ObstaclesBuffer = Shader.PropertyToID("obstaclesBuffer");
        private static readonly int NumObstacles = Shader.PropertyToID("numObstacles");
        private static readonly int CameraFrustumPlanes = Shader.PropertyToID("cameraFrustumPlanes");
        private static readonly int CullingRadius = Shader.PropertyToID("cullingRadius");
        private static readonly int VisibleBoidIndices = Shader.PropertyToID("VisibleBoidIndices");
        
        // THE FIX: Restored the material buffer ID
        private static readonly int BoidsBuffer = Shader.PropertyToID("boidsBuffer");
        private static readonly int BoidIndices = Shader.PropertyToID("visibleBoidIndices");

        [Header("Core References")]
        public ComputeShader baseBoidsCompute; 
        public ComputeShader radixSortCompute;
        
        private CommandBuffer asyncComputeCmd;
        private MaterialPropertyBlock propertyBlock;

        private BoidSwarm[] swarms;
        private Camera _cam;

        // THE FIX: Cached Kernel IDs
        private int _kFreeRoam, _kSpline, _kClearGrid, _kPopulate, _kBuildOffsets, _kReorder, _kCull;

        private void Awake() => _cam = Camera.main;

        private void Start()
        {
            propertyBlock = new MaterialPropertyBlock();
            swarms = FindObjectsByType<BoidSwarm>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            
            // Inject the base shader so each swarm can instance its own copy
            foreach (BoidSwarm swarm in swarms) swarm.Initialize(baseBoidsCompute);
            
            // Cache Kernels ONCE at startup (O(1) execution instead of per-frame polling)
            _kFreeRoam = baseBoidsCompute.FindKernel("CSMain");
            _kSpline = baseBoidsCompute.FindKernel("CSMain_SplineFlow");
            _kClearGrid = baseBoidsCompute.FindKernel("ClearGrid");
            _kPopulate = baseBoidsCompute.FindKernel("PopulateHashes");
            _kBuildOffsets = baseBoidsCompute.FindKernel("BuildGridOffsets");
            _kReorder = baseBoidsCompute.FindKernel("ReorderBoids");
            _kCull = baseBoidsCompute.FindKernel("FrustumCull");
            
            asyncComputeCmd = new CommandBuffer { name = "Boids Async Physics" };
            asyncComputeCmd.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        }

        private void Update()
        {
            if (swarms.Length == 0) return;

            asyncComputeCmd.Clear();
            asyncComputeCmd.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

            // Frustum Culling calculation (Once per frame for all swarms)
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_cam);
            Vector4[] frustumPlanes = new Vector4[6];
            for (int i = 0; i < 6; i++) {
                frustumPlanes[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
            }

            foreach (BoidSwarm swarm in swarms)
            {
                swarm.SyncEnvironmentData();
                bool shouldSort = (Time.frameCount + swarm.frameOffset) % swarm.sortFrequency == 0;

                // Grab the Compute Shader specific to this swarm
                ComputeShader localCompute = swarm.SwarmCompute;
                
                // Set continuous Time variables explicitly to this swarm
                asyncComputeCmd.SetComputeVectorArrayParam(localCompute, CameraFrustumPlanes, frustumPlanes);
                asyncComputeCmd.SetComputeFloatParam(localCompute, CullingRadius, 1.5f);
                asyncComputeCmd.SetComputeFloatParam(localCompute, DeltaTime, Time.deltaTime);
                asyncComputeCmd.SetComputeFloatParam(localCompute, Time1, Time.time);

                // ==========================================
                // SORTING COMMANDS
                // ==========================================
                if (shouldSort)
                {
                    int totalCells = swarm.gridSize * swarm.gridSize * swarm.gridSize;
                    
                    asyncComputeCmd.SetComputeBufferParam(localCompute, _kClearGrid, GridOffsets, swarm.gridOffsets);
                    asyncComputeCmd.DispatchCompute(localCompute, _kClearGrid, Mathf.CeilToInt(totalCells / 64f), 1, 1);

                    asyncComputeCmd.SetComputeBufferParam(localCompute, _kPopulate, ReadBoidsBuffer, swarm.readBuffer);
                    asyncComputeCmd.SetComputeBufferParam(localCompute, _kPopulate, SortBuffer, swarm.sortBuffer);
                    asyncComputeCmd.SetComputeIntParam(localCompute, NumBoids, swarm.boidCount);
                    asyncComputeCmd.SetComputeIntParam(localCompute, PaddedCount, swarm.paddedCount);
                    asyncComputeCmd.DispatchCompute(localCompute, _kPopulate, Mathf.CeilToInt(swarm.paddedCount / 64f), 1, 1);

                    // Execute Radix Sort
                    GPUSort.RadixSort(asyncComputeCmd, radixSortCompute, swarm.sortBuffer, swarm.tempSortBuffer, swarm.globalHistBuffer, swarm.localOffsetsBuffer, swarm.paddedCount);

                    asyncComputeCmd.SetComputeBufferParam(localCompute, _kBuildOffsets, SortBuffer, swarm.sortBuffer);
                    asyncComputeCmd.SetComputeBufferParam(localCompute, _kBuildOffsets, GridOffsets, swarm.gridOffsets);
                    asyncComputeCmd.DispatchCompute(localCompute, _kBuildOffsets, Mathf.CeilToInt(swarm.boidCount / 64f), 1, 1);

                    asyncComputeCmd.SetComputeBufferParam(localCompute, _kReorder, ReadBoidsBuffer, swarm.readBuffer);
                    asyncComputeCmd.SetComputeBufferParam(localCompute, _kReorder, WriteBoidsBuffer, swarm.writeBuffer);
                    asyncComputeCmd.SetComputeBufferParam(localCompute, _kReorder, SortBuffer, swarm.sortBuffer);
                    asyncComputeCmd.DispatchCompute(localCompute, _kReorder, Mathf.CeilToInt(swarm.boidCount / 64f), 1, 1);

                    swarm.PingPongBuffers();
                }

                // ==========================================
                // MAIN SIMULATION DISPATCH
                // ==========================================
                int activeKernel = swarm.followSpline ? _kSpline : _kFreeRoam;

                if (swarm.followSpline && swarm.splineBuffer != null)
                {
                    asyncComputeCmd.SetComputeBufferParam(localCompute, activeKernel, SplineBuffer, swarm.splineBuffer);
                    asyncComputeCmd.SetComputeIntParam(localCompute, SplineResolution, swarm.splineResolution);
                    asyncComputeCmd.SetComputeFloatParam(localCompute, SplineLength, swarm.splineLength);
                }

                asyncComputeCmd.SetComputeBufferParam(localCompute, activeKernel, AttractorsBuffer, swarm.attractorsBuffer);
                asyncComputeCmd.SetComputeIntParam(localCompute, NumAttractors, swarm.CurrentAttractorCount);
                asyncComputeCmd.SetComputeBufferParam(localCompute, activeKernel, PredatorsBuffer, swarm.predatorsBuffer);
                asyncComputeCmd.SetComputeIntParam(localCompute, NumPredators, swarm.CurrentPredatorCount);
                asyncComputeCmd.SetComputeBufferParam(localCompute, activeKernel, ObstaclesBuffer, swarm.obstaclesBuffer);
                asyncComputeCmd.SetComputeIntParam(localCompute, NumObstacles, swarm.CurrentObstacleCount);
                
                asyncComputeCmd.SetComputeIntParam(localCompute, NumBoids, swarm.boidCount);
                asyncComputeCmd.SetComputeBufferParam(localCompute, activeKernel, ReadBoidsBuffer, swarm.readBuffer);
                asyncComputeCmd.SetComputeBufferParam(localCompute, activeKernel, WriteBoidsBuffer, swarm.writeBuffer);
                asyncComputeCmd.SetComputeBufferParam(localCompute, activeKernel, GridOffsets, swarm.gridOffsets);
                asyncComputeCmd.SetComputeBufferParam(localCompute, activeKernel, SortBuffer, swarm.sortBuffer);

                asyncComputeCmd.DispatchCompute(localCompute, activeKernel, Mathf.CeilToInt(swarm.boidCount / 64f), 1, 1);
                
                // ==========================================
                // APPEND CULLING DISPATCH
                // ==========================================
                asyncComputeCmd.SetBufferCounterValue(swarm.visibleBoidsBuffer, 0);
                asyncComputeCmd.SetComputeIntParam(localCompute, NumBoids, swarm.boidCount);
                asyncComputeCmd.SetComputeBufferParam(localCompute, _kCull, ReadBoidsBuffer, swarm.readBuffer);
                asyncComputeCmd.SetComputeBufferParam(localCompute, _kCull, BoidIndices, swarm.visibleBoidsBuffer);
                asyncComputeCmd.DispatchCompute(localCompute, _kCull, Mathf.CeilToInt(swarm.boidCount / 64f), 1, 1);

                asyncComputeCmd.CopyCounterValue(swarm.visibleBoidsBuffer, swarm.argsBuffer, 4);
                
                swarm.PingPongBuffers();
            }

            // Execute the heavily optimized Command Buffer
            Graphics.ExecuteCommandBufferAsync(asyncComputeCmd, ComputeQueueType.Default);

            // ==========================================
            // GRAPHICS QUEUE RENDERING
            // ==========================================
            foreach (BoidSwarm swarm in swarms)
            {
                propertyBlock.Clear();
                propertyBlock.SetBuffer(BoidsBuffer, swarm.readBuffer);
                propertyBlock.SetBuffer(VisibleBoidIndices, swarm.visibleBoidsBuffer);

                RenderParams renderParams = new(swarm.swarmMaterial)
                {
                    worldBounds = swarm.swarmBounds,
                    matProps = propertyBlock,
                    shadowCastingMode = ShadowCastingMode.On
                };

                Graphics.RenderMeshIndirect(renderParams, swarm.swarmMesh, swarm.argsBuffer);
            }
        }

        private void OnDestroy() => asyncComputeCmd?.Release();
    }
}