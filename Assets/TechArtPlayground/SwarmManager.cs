using UnityEngine;
using UnityEngine.Rendering;

namespace TechArtPlayground
{
    public class SwarmManager : MonoBehaviour
    {
        private static readonly int ReadBoidsBuffer = Shader.PropertyToID("ReadBoidsBuffer");
        private static readonly int SortBuffer = Shader.PropertyToID("SortBuffer");
        private static readonly int GridOffsets = Shader.PropertyToID("gridOffsets");
        private static readonly int NumBoids = Shader.PropertyToID("numBoids");
        private static readonly int PaddedCount = Shader.PropertyToID("paddedCount");
        private static readonly int WriteBoidsBuffer = Shader.PropertyToID("WriteBoidsBuffer");
        private static readonly int SplineBuffer = Shader.PropertyToID("splineBuffer");
        private static readonly int DeltaTime = Shader.PropertyToID("deltaTime");
        private static readonly int Speed = Shader.PropertyToID("speed");
        private static readonly int SightRadius = Shader.PropertyToID("sightRadius");
        private static readonly int BoidsBuffer = Shader.PropertyToID("boidsBuffer");

        private static readonly int SplineResolution = Shader.PropertyToID("splineResolution");
        private static readonly int SplineLength = Shader.PropertyToID("splineLength");
        private static readonly int AttractorsBuffer = Shader.PropertyToID("attractorsBuffer");
        private static readonly int NumAttractors = Shader.PropertyToID("numAttractors");
        private static readonly int PredatorsBuffer = Shader.PropertyToID("predatorsBuffer");
        private static readonly int NumPredators = Shader.PropertyToID("numPredators");
        private static readonly int ObstaclesBuffer = Shader.PropertyToID("obstaclesBuffer");
        private static readonly int NumObstacles = Shader.PropertyToID("numObstacles");
        private static readonly int Time1 = Shader.PropertyToID("time");
        private static readonly int GridSize = Shader.PropertyToID("gridSize");
        private static readonly int CellSize = Shader.PropertyToID("cellSize");
        private static readonly int AlignmentWeight = Shader.PropertyToID("alignmentWeight");
        private static readonly int SeparationWeight = Shader.PropertyToID("separationWeight");
        private static readonly int CohesionWeight = Shader.PropertyToID("cohesionWeight");
        private static readonly int FloorY = Shader.PropertyToID("floorY");
        private static readonly int AvoidanceMargin = Shader.PropertyToID("avoidanceMargin");
        private static readonly int PredatorFleeWeight = Shader.PropertyToID("predatorFleeWeight");
        private static readonly int TubeRadius = Shader.PropertyToID("tubeRadius");
        private static readonly int SingularitySoften = Shader.PropertyToID("singularitySoften");
        private static readonly int ArrivalMinSpeed = Shader.PropertyToID("arrivalMinSpeed");
        private static readonly int ArrivalRadiusSq = Shader.PropertyToID("arrivalRadiusSq");
        private static readonly int SwirlStrength = Shader.PropertyToID("swirlStrength");
        private static readonly int TargetWeight = Shader.PropertyToID("targetWeight");
        private static readonly int TargetPosition = Shader.PropertyToID("targetPosition");

        [Header("Core References")]
        public ComputeShader boidsCompute;
        public ComputeShader radixSortCompute;
        private CommandBuffer asyncComputeCmd; // NEW
        
        private MaterialPropertyBlock propertyBlock;


        private BoidSwarm[] swarms;


        private void Start()
        {
            propertyBlock = new MaterialPropertyBlock();
            swarms = FindObjectsByType<BoidSwarm>();
            foreach (BoidSwarm swarm in swarms) swarm.Initialize();
            
            // Initialize Command Buffer and flag for Async execution
            asyncComputeCmd = new CommandBuffer { name = "Boids Async Physics" };
            asyncComputeCmd.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        }

        private void Update()
        {
            if (swarms.Length == 0) return;

            int kernelFreeRoam = boidsCompute.FindKernel("CSMain");
            int kernelSpline = boidsCompute.FindKernel("CSMain_SplineFlow");
            int kernelClearGrid = boidsCompute.FindKernel("ClearGrid");
            int kernelPopulate = boidsCompute.FindKernel("PopulateHashes");
            int kernelBuildOffsets = boidsCompute.FindKernel("BuildGridOffsets");
            int kernelReorder = boidsCompute.FindKernel("ReorderBoids");

            // 1. Clear the command buffer for this frame
            asyncComputeCmd.Clear();

            asyncComputeCmd.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

            foreach (BoidSwarm swarm in swarms)
            {
                swarm.SyncEnvironmentData();
                bool shouldSort = (Time.frameCount + swarm.frameOffset) % swarm.sortFrequency == 0;

                // ==========================================
                // RECORD ASYNC COMPUTE COMMANDS
                // ==========================================
                if (shouldSort)
                {
                    int totalCells = swarm.gridSize * swarm.gridSize * swarm.gridSize;
                    
                    asyncComputeCmd.SetComputeBufferParam(boidsCompute, kernelClearGrid, GridOffsets, swarm.gridOffsets);
                    asyncComputeCmd.DispatchCompute(boidsCompute, kernelClearGrid, Mathf.CeilToInt(totalCells / 64f), 1, 1);

                    asyncComputeCmd.SetComputeBufferParam(boidsCompute, kernelPopulate, ReadBoidsBuffer, swarm.readBuffer);
                    asyncComputeCmd.SetComputeBufferParam(boidsCompute, kernelPopulate, SortBuffer, swarm.sortBuffer);
                    asyncComputeCmd.SetComputeIntParam(boidsCompute, NumBoids, swarm.boidCount);
                    asyncComputeCmd.SetComputeIntParam(boidsCompute, PaddedCount, swarm.paddedCount);
                    asyncComputeCmd.DispatchCompute(boidsCompute, kernelPopulate, Mathf.CeilToInt(swarm.paddedCount / 64f), 1, 1);

                    // Sort using the async command buffer
                    GPUSort.RadixSort(asyncComputeCmd, radixSortCompute, swarm.sortBuffer, swarm.tempSortBuffer, swarm.globalHistBuffer, swarm.localOffsetsBuffer, swarm.paddedCount);

                    asyncComputeCmd.SetComputeBufferParam(boidsCompute, kernelBuildOffsets, SortBuffer, swarm.sortBuffer);
                    asyncComputeCmd.SetComputeBufferParam(boidsCompute, kernelBuildOffsets, GridOffsets, swarm.gridOffsets);
                    asyncComputeCmd.DispatchCompute(boidsCompute, kernelBuildOffsets, Mathf.CeilToInt(swarm.boidCount / 64f), 1, 1);

                    asyncComputeCmd.SetComputeBufferParam(boidsCompute, kernelReorder, ReadBoidsBuffer, swarm.readBuffer);
                    asyncComputeCmd.SetComputeBufferParam(boidsCompute, kernelReorder, WriteBoidsBuffer, swarm.writeBuffer);
                    asyncComputeCmd.SetComputeBufferParam(boidsCompute, kernelReorder, SortBuffer, swarm.sortBuffer);
                    asyncComputeCmd.DispatchCompute(boidsCompute, kernelReorder, Mathf.CeilToInt(swarm.boidCount / 64f), 1, 1);

                    swarm.PingPongBuffers();
                }

                int activeKernel = swarm.followSpline ? kernelSpline : kernelFreeRoam;

                if (swarm.followSpline && swarm.splineBuffer != null)
                {
                    asyncComputeCmd.SetComputeBufferParam(boidsCompute, activeKernel, SplineBuffer, swarm.splineBuffer);
                    asyncComputeCmd.SetComputeIntParam(boidsCompute, SplineResolution, swarm.splineResolution);
                    asyncComputeCmd.SetComputeFloatParam(boidsCompute, SplineLength, swarm.splineLength);
                }

                asyncComputeCmd.SetComputeBufferParam(boidsCompute, activeKernel, AttractorsBuffer, swarm.attractorsBuffer);
                asyncComputeCmd.SetComputeIntParam(boidsCompute, NumAttractors, swarm.CurrentAttractorCount);
                asyncComputeCmd.SetComputeBufferParam(boidsCompute, activeKernel, PredatorsBuffer, swarm.predatorsBuffer);
                asyncComputeCmd.SetComputeIntParam(boidsCompute, NumPredators, swarm.CurrentPredatorCount);
                asyncComputeCmd.SetComputeBufferParam(boidsCompute, activeKernel, ObstaclesBuffer, swarm.obstaclesBuffer);
                asyncComputeCmd.SetComputeIntParam(boidsCompute, NumObstacles, 0);
                
                asyncComputeCmd.SetComputeIntParam(boidsCompute, NumBoids, swarm.boidCount);
                asyncComputeCmd.SetComputeBufferParam(boidsCompute, activeKernel, ReadBoidsBuffer, swarm.readBuffer);
                asyncComputeCmd.SetComputeBufferParam(boidsCompute, activeKernel, WriteBoidsBuffer, swarm.writeBuffer);
                asyncComputeCmd.SetComputeBufferParam(boidsCompute, activeKernel, GridOffsets, swarm.gridOffsets);
                asyncComputeCmd.SetComputeBufferParam(boidsCompute, activeKernel, SortBuffer, swarm.sortBuffer);

                asyncComputeCmd.SetComputeFloatParam(boidsCompute, DeltaTime, Time.deltaTime);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, Time1, Time.time);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, CellSize, swarm.cellSize);
                asyncComputeCmd.SetComputeIntParam(boidsCompute, GridSize, swarm.gridSize);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, Speed, swarm.speed);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, SightRadius, swarm.sightRadius);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, SeparationWeight, swarm.separationWeight);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, AlignmentWeight, swarm.alignmentWeight);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, CohesionWeight, swarm.cohesionWeight);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, FloorY, swarm.floorY);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, AvoidanceMargin, swarm.avoidanceMargin);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, PredatorFleeWeight, swarm.predatorFleeWeight);
                
                asyncComputeCmd.SetComputeVectorParam(boidsCompute, TargetPosition, swarm.defaultWaypoint);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, TargetWeight, swarm.targetWeight);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, SwirlStrength, swarm.swirlStrength);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, ArrivalRadiusSq, swarm.arrivalRadiusSq);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, ArrivalMinSpeed, swarm.arrivalMinSpeed);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, SingularitySoften, swarm.singularitySoften);
                asyncComputeCmd.SetComputeFloatParam(boidsCompute, TubeRadius, swarm.tubeRadius);

                asyncComputeCmd.DispatchCompute(boidsCompute, activeKernel, Mathf.CeilToInt(swarm.boidCount / 64f), 1, 1);
                
                // Note: The Ping-pong occurs immediately on the CPU side. Since CommandBuffer records the buffer reference
                // at the time 'SetComputeBufferParam' is called, this is completely safe and guarantees the graphics queue
                // renders the currently calculated frame while the compute queue calculates the next one.
                swarm.PingPongBuffers();
            }

            // 2. Dispatch the Command Buffer to the GPU's Async Compute Queue
            Graphics.ExecuteCommandBufferAsync(asyncComputeCmd, ComputeQueueType.Default);
            // 3. GRAPHICS QUEUE RENDERING
            // ==========================================
            foreach (BoidSwarm swarm in swarms)
            {
                propertyBlock.Clear();
                // Because we ping-ponged immediately above, we bind the newly calculated readBuffer for rendering
                propertyBlock.SetBuffer(BoidsBuffer, swarm.readBuffer);
                
                RenderParams renderParams = new(swarm.swarmMaterial)
                {
                    worldBounds = swarm.swarmBounds,
                    matProps = propertyBlock,
                    shadowCastingMode = ShadowCastingMode.On
                };

                Graphics.RenderMeshIndirect(renderParams, swarm.swarmMesh, swarm.argsBuffer);
            }
        }
        private void OnDestroy()
        {
            asyncComputeCmd?.Release();
        }
    }
}