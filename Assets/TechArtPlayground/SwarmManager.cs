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
        
        private MaterialPropertyBlock propertyBlock;


        private BoidSwarm[] swarms;


        private void Start()
        {
            propertyBlock = new MaterialPropertyBlock();
            swarms = FindObjectsByType<BoidSwarm>(FindObjectsSortMode.None);
            foreach (BoidSwarm swarm in swarms) swarm.Initialize(); // Mesh is now handled internally by the swarm
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

            foreach (BoidSwarm swarm in swarms)
            {
                swarm.SyncEnvironmentData();

                // ==========================================
                // 1. SPATIAL GRID & SORTING (Amortized per Swarm)
                // ==========================================
                // Check if this specific swarm is scheduled to sort this frame
                bool shouldSort = (Time.frameCount + swarm.frameOffset) % swarm.sortFrequency == 0;

                if (shouldSort)
                {
                    // Clear Grid
                    int totalCells = swarm.gridSize * swarm.gridSize * swarm.gridSize;
                    boidsCompute.SetBuffer(kernelClearGrid, "gridOffsets", swarm.gridOffsets);
                    boidsCompute.Dispatch(kernelClearGrid, Mathf.CeilToInt(totalCells / 64f), 1, 1);

                    // Populate Hashes
                    boidsCompute.SetBuffer(kernelPopulate, "ReadBoidsBuffer", swarm.readBuffer);
                    boidsCompute.SetBuffer(kernelPopulate, "SortBuffer", swarm.sortBuffer);
                    boidsCompute.SetInt("numBoids", swarm.boidCount);
                    boidsCompute.SetInt("paddedCount", swarm.paddedCount);
                    boidsCompute.Dispatch(kernelPopulate, Mathf.CeilToInt(swarm.paddedCount / 64f), 1, 1);

                    // Sort (Using GraphicsBuffer natively)
                    GPUSort.RadixSort(
                        radixSortCompute, 
                        swarm.sortBuffer, 
                        swarm.tempSortBuffer, 
                        swarm.globalHistBuffer, 
                        swarm.localOffsetsBuffer, 
                        swarm.paddedCount
                    );

                    // Build Offsets & Reorder
                    boidsCompute.SetBuffer(kernelBuildOffsets, "SortBuffer", swarm.sortBuffer);
                    boidsCompute.SetBuffer(kernelBuildOffsets, "gridOffsets", swarm.gridOffsets);
                    boidsCompute.Dispatch(kernelBuildOffsets, Mathf.CeilToInt(swarm.boidCount / 64f), 1, 1);

                    boidsCompute.SetBuffer(kernelReorder, "ReadBoidsBuffer", swarm.readBuffer);
                    boidsCompute.SetBuffer(kernelReorder, "WriteBoidsBuffer", swarm.writeBuffer);
                    boidsCompute.SetBuffer(kernelReorder, "SortBuffer", swarm.sortBuffer);
                    boidsCompute.Dispatch(kernelReorder, Mathf.CeilToInt(swarm.boidCount / 64f), 1, 1);

                    // Ping-pong sorted data to ReadBuffer so Physics can use it
                    swarm.PingPongBuffers();
                }

                // ==========================================
                // 2. FLOCKING / SPLINE PHYSICS
                // ==========================================
                int activeKernel = swarm.followSpline ? kernelSpline : kernelFreeRoam;

// --- NEW: Bind Spline Data if active ---
                if (swarm.followSpline && swarm.splineBuffer != null)
                {
                    boidsCompute.SetBuffer(activeKernel, SplineBuffer, swarm.splineBuffer);
                    boidsCompute.SetInt(SplineResolution, swarm.splineResolution);
                    boidsCompute.SetFloat(SplineLength, swarm.splineLength);
                }


// Bind Environment Buffers & Counts (Using the dynamic counts!)
                boidsCompute.SetBuffer(activeKernel, AttractorsBuffer, swarm.attractorsBuffer);
                boidsCompute.SetInt(NumAttractors, swarm.CurrentAttractorCount);

                boidsCompute.SetBuffer(activeKernel, PredatorsBuffer, swarm.predatorsBuffer);
                boidsCompute.SetInt(NumPredators, swarm.CurrentPredatorCount);

                boidsCompute.SetBuffer(activeKernel, ObstaclesBuffer, swarm.obstaclesBuffer);
                boidsCompute.SetInt(NumObstacles, 0);

// Bind Core Buffers
                boidsCompute.SetInt(NumBoids, swarm.boidCount);
                boidsCompute.SetBuffer(activeKernel, ReadBoidsBuffer, swarm.readBuffer);
                boidsCompute.SetBuffer(activeKernel, WriteBoidsBuffer, swarm.writeBuffer);
                boidsCompute.SetBuffer(activeKernel, GridOffsets, swarm.gridOffsets);
                boidsCompute.SetBuffer(activeKernel, SortBuffer, swarm.sortBuffer);

// Time & Grid Mechanics
                boidsCompute.SetFloat(DeltaTime, Time.deltaTime);
                boidsCompute.SetFloat(Time1, Time.time);
                boidsCompute.SetFloat(CellSize, swarm.cellSize);
                boidsCompute.SetInt(GridSize, swarm.gridSize);
// Flocking Physics
                boidsCompute.SetFloat(Speed, swarm.speed);
                boidsCompute.SetFloat(SightRadius, swarm.sightRadius);
                boidsCompute.SetFloat(SeparationWeight, swarm.separationWeight);
                boidsCompute.SetFloat(AlignmentWeight, swarm.alignmentWeight);
                boidsCompute.SetFloat(CohesionWeight, swarm.cohesionWeight);

// Environment
                boidsCompute.SetFloat(FloorY, swarm.floorY);
                boidsCompute.SetFloat(AvoidanceMargin, swarm.avoidanceMargin);
                boidsCompute.SetFloat(PredatorFleeWeight, swarm.predatorFleeWeight);

// Attractors
                boidsCompute.SetVector(TargetPosition, swarm.defaultWaypoint);
                boidsCompute.SetFloat(TargetWeight, swarm.targetWeight);
                boidsCompute.SetFloat(SwirlStrength, swarm.swirlStrength);
                boidsCompute.SetFloat(ArrivalRadiusSq, swarm.arrivalRadiusSq);
                boidsCompute.SetFloat(ArrivalMinSpeed, swarm.arrivalMinSpeed);
                boidsCompute.SetFloat(SingularitySoften, swarm.singularitySoften);

// Spline (If applicable)
                boidsCompute.SetFloat(TubeRadius, swarm.tubeRadius);
// boidsCompute.SetFloat("splineLength", swarm.splineLength); // Ensure you pass this if following splines!

                boidsCompute.SetInt(NumBoids, swarm.boidCount);

// Finally, dispatch the compute shader
                boidsCompute.Dispatch(activeKernel, Mathf.CeilToInt(swarm.boidCount / 64f), 1, 1);

                swarm.PingPongBuffers();

                // 3. Rendering (Now using per-swarm materials and meshes)
                propertyBlock.Clear();
                propertyBlock.SetBuffer(BoidsBuffer, swarm.readBuffer);
                
                RenderParams renderParams = new(swarm.swarmMaterial)
                {
                    worldBounds = swarm.swarmBounds,
                    matProps = propertyBlock,
                    shadowCastingMode = ShadowCastingMode.On
                };



                // Issue the draw call for THIS specific mesh
                Graphics.RenderMeshIndirect(renderParams, swarm.swarmMesh, swarm.argsBuffer);
            }
        }
    }
}