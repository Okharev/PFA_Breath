using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Splines;
using Random = UnityEngine.Random;

namespace TechArtPlayground
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct SplineSampleData
    {
        public Vector3 position;
        public Vector3 tangent;
        public float width;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ObstacleData
    {
        public Vector3 position;
        public int type;
        public Vector3 extents;
        public float padding;
        public Vector4 rotation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Boid
    {
        public Vector3 position;
        public float randomSeed;
        public Vector3 velocity;
        public float colorSeed;
        public uint packedData;
        public float splineT;
        public float pad1;
        public float pad2;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct AttractorData
    {
        public Vector3 position;
        public float weight;
        public Vector3 velocity;
        public float padding;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct PredatorData
    {
        public Vector3 position;
        public float radiusSq;
    }

    public class BoidSwarm : MonoBehaviour
    {
        // Compute Shader Cache IDs
        private static readonly int SpeedID = Shader.PropertyToID("speed");
        private static readonly int SightRadiusID = Shader.PropertyToID("sightRadius");
        private static readonly int SeparationWeightID = Shader.PropertyToID("separationWeight");
        private static readonly int AlignmentWeightID = Shader.PropertyToID("alignmentWeight");
        private static readonly int CohesionWeightID = Shader.PropertyToID("cohesionWeight");
        private static readonly int FloorYID = Shader.PropertyToID("floorY");
        private static readonly int AvoidanceMarginID = Shader.PropertyToID("avoidanceMargin");
        private static readonly int PredatorFleeWeightID = Shader.PropertyToID("predatorFleeWeight");
        private static readonly int TubeRadiusID = Shader.PropertyToID("tubeRadius");
        private static readonly int SingularitySoftenID = Shader.PropertyToID("singularitySoften");
        private static readonly int ArrivalMinSpeedID = Shader.PropertyToID("arrivalMinSpeed");
        private static readonly int ArrivalRadiusSqID = Shader.PropertyToID("arrivalRadiusSq");
        private static readonly int SwirlStrengthID = Shader.PropertyToID("swirlStrength");
        private static readonly int TargetWeightID = Shader.PropertyToID("targetWeight");
        private static readonly int TargetPositionID = Shader.PropertyToID("targetPosition");
        private static readonly int CellSizeID = Shader.PropertyToID("cellSize");
        private static readonly int GridSizeID = Shader.PropertyToID("gridSize");

        [Header("Rendering")] 
        public Mesh swarmMesh;
        public Material swarmMaterial;

        [Header("Swarm Configuration")] 
        public int boidCount = 5000;
        public Bounds swarmBounds = new Bounds(Vector3.zero, new Vector3(100, 100, 100));
        public bool followSpline;

        [Header("Dynamic Environment Limits")] 
        public int maxAttractors = 16;
        public int maxPredators = 16;
        public int maxObstacles = 32;

        [Header("Spline Settings")] 
        public SplineContainer splineContainer;
        public int splineResolution = 100;
        
        [Range(0.1f, 10f)] [SerializeField] private float tubeRadius = 2.0f;
        public float TubeRadius { get => tubeRadius; set { tubeRadius = value; PushFloat(TubeRadiusID, value); } }
        public float splineLength { get; private set; }

        [Header("Flocking Behaviors")] 
        [Range(0.1f, 20f)] [SerializeField] private float speed = 4.0f;
        public float Speed { get => speed; set { speed = value; PushFloat(SpeedID, value); } }

        [Range(0.1f, 10f)] [SerializeField] private float sightRadius = 2.5f;
        public float SightRadius { get => sightRadius; set { sightRadius = value; PushFloat(SightRadiusID, value); } }

        [Range(0.0f, 5f)] [SerializeField] private float separationWeight = 1.5f;
        public float SeparationWeight { get => separationWeight; set { separationWeight = value; PushFloat(SeparationWeightID, value); } }

        [Range(0.0f, 5f)] [SerializeField] private float alignmentWeight = 1.0f;
        public float AlignmentWeight { get => alignmentWeight; set { alignmentWeight = value; PushFloat(AlignmentWeightID, value); } }

        [Range(0.0f, 5f)] [SerializeField] private float cohesionWeight = 1.5f;
        public float CohesionWeight { get => cohesionWeight; set { cohesionWeight = value; PushFloat(CohesionWeightID, value); } }

        [Header("Environment & Avoidance")] 
        [SerializeField] private float floorY = -10f;
        public float FloorY { get => floorY; set { floorY = value; PushFloat(FloorYID, value); } }

        [SerializeField] private float avoidanceMargin = 2.0f;
        public float AvoidanceMargin { get => avoidanceMargin; set { avoidanceMargin = value; PushFloat(AvoidanceMarginID, value); } }

        [Range(0f, 10f)] [SerializeField] private float predatorFleeWeight = 5.0f;
        public float PredatorFleeWeight { get => predatorFleeWeight; set { predatorFleeWeight = value; PushFloat(PredatorFleeWeightID, value); } }

        [Header("Attractors & Waypoints")] 
        [SerializeField] private Vector3 targetPosition = Vector3.zero;
        public Vector3 TargetPosition { get => targetPosition; set { targetPosition = value; PushVector(TargetPositionID, value); } }

        [Range(0f, 5f)] [SerializeField] private float targetWeight = 1.0f;
        public float TargetWeight { get => targetWeight; set { targetWeight = value; PushFloat(TargetWeightID, value); } }

        [SerializeField] private float swirlStrength = 2.0f;
        public float SwirlStrength { get => swirlStrength; set { swirlStrength = value; PushFloat(SwirlStrengthID, value); } }

        [SerializeField] private float arrivalRadiusSq = 25.0f; 
        public float ArrivalRadiusSq { get => arrivalRadiusSq; set { arrivalRadiusSq = value; PushFloat(ArrivalRadiusSqID, value); } }

        [SerializeField] private float arrivalMinSpeed = 0.5f;
        public float ArrivalMinSpeed { get => arrivalMinSpeed; set { arrivalMinSpeed = value; PushFloat(ArrivalMinSpeedID, value); } }

        [SerializeField] private float singularitySoften = 1.0f;
        public float SingularitySoften { get => singularitySoften; set { singularitySoften = value; PushFloat(SingularitySoftenID, value); } }

        [Header("Optimization")]
        [Range(1, 10)] public int sortFrequency = 4;

        [Header("Spatial Grid Tuning")] 
        [SerializeField] private float cellSize = 3.0f;
        public float CellSize { get => cellSize; set { cellSize = value; PushFloat(CellSizeID, value); } }

        [SerializeField] private int gridSize = 64;
        public int GridSize { get => gridSize; set { gridSize = value; PushInt(GridSizeID, value); } }


        // --- INTERNAL TRACKING ---
        private readonly List<BoidAttractor> activeAttractors = new();
        private readonly List<BoidPredator> activePredators = new();
        private readonly List<BoidObstacle> activeObstacles = new();

        private AttractorData[] attractorDataCache;
        private PredatorData[] predatorDataCache;
        private ObstacleData[] obstacleDataCache;
        private Vector3[] previousAttractorPositions;

        public int frameOffset { get; private set; }
        public int paddedCount { get; private set; }
        public ComputeShader SwarmCompute { get; private set; }

        public int CurrentAttractorCount => Mathf.Min(activeAttractors.Count, maxAttractors);
        public int CurrentPredatorCount => Mathf.Min(activePredators.Count, maxPredators);
        public int CurrentObstacleCount => Mathf.Min(activeObstacles.Count, maxObstacles);

        // --- BUFFERS ---
        public GraphicsBuffer readBuffer { get; private set; }
        public GraphicsBuffer writeBuffer { get; private set; }
        public GraphicsBuffer sortBuffer { get; private set; }
        public GraphicsBuffer gridOffsets { get; private set; }
        public GraphicsBuffer splineBuffer { get; private set; }
        public GraphicsBuffer argsBuffer { get; private set; }
        public GraphicsBuffer tempSortBuffer { get; private set; }
        public GraphicsBuffer globalHistBuffer { get; private set; }
        public GraphicsBuffer localOffsetsBuffer { get; private set; }
        public GraphicsBuffer attractorsBuffer { get; private set; }
        public GraphicsBuffer predatorsBuffer { get; private set; }
        public GraphicsBuffer obstaclesBuffer { get; private set; }
        public GraphicsBuffer visibleBoidsBuffer { get; private set; }

        public void Initialize(ComputeShader baseCompute)
        {
            ReleaseAllBuffers();

            // 1. ISOLATE THE COMPUTE SHADER
            SwarmCompute = Instantiate(baseCompute);
            SwarmCompute.name = "BoidsCompute_" + gameObject.name;

            targetPosition = transform.position;
            frameOffset = Random.Range(0, 10);
            paddedCount = Mathf.NextPowerOfTwo(boidCount);

            // Initialize Caches
            attractorDataCache = new AttractorData[maxAttractors];
            predatorDataCache = new PredatorData[maxPredators];
            obstacleDataCache = new ObstacleData[maxObstacles];
            previousAttractorPositions = new Vector3[maxAttractors];

            // Allocate Buffers
            int safeAttCount = Mathf.Max(1, maxAttractors);
            int safePredCount = Mathf.Max(1, maxPredators);
            int safeObsCount = Mathf.Max(1, maxObstacles);

            attractorsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, safeAttCount, 32);
            predatorsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, safePredCount, 16);
            obstaclesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, safeObsCount, 48);

            readBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidCount, 48);
            writeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidCount, 48);
            sortBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, paddedCount, 8);
            visibleBoidsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Append, boidCount, 4);

            int numBlocks = Mathf.Max(1, Mathf.CeilToInt(paddedCount / 256f));
            tempSortBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, paddedCount, 8); 
            globalHistBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 16 * numBlocks, 4);
            localOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, paddedCount, 4);

            int totalGridCells = gridSize * gridSize * gridSize;
            gridOffsets = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalGridCells, 4);

            argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(uint));
            uint[] args = new uint[5];
            if (swarmMesh != null)
            {
                args[0] = swarmMesh.GetIndexCount(0);
                args[1] = (uint)boidCount;
                args[2] = swarmMesh.GetIndexStart(0);
                args[3] = swarmMesh.GetBaseVertex(0);
                args[4] = 0;
            }
            argsBuffer.SetData(args);

            if (followSpline && splineContainer != null)
            {
                splineLength = splineContainer.CalculateLength();
                SplineSampleData[] bakedSpline = new SplineSampleData[splineResolution];

                for (int i = 0; i < splineResolution; i++)
                {
                    float t = i / (float)(splineResolution - 1);
                    bakedSpline[i] = new SplineSampleData
                    {
                        position = splineContainer.EvaluatePosition(t),
                        tangent = ((Vector3)splineContainer.EvaluateTangent(t)).normalized,
                        width = tubeRadius
                    };
                }
                splineBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, splineResolution, 28);
                splineBuffer.SetData(bakedSpline);
            }

            PopulateInitialData();
            PushAllComputeData();
        }

        // --- HELPER METHODS FOR STATE SYNC ---
        private void PushFloat(int id, float val) { if (SwarmCompute != null) SwarmCompute.SetFloat(id, val); }
        private void PushInt(int id, int val) { if (SwarmCompute != null) SwarmCompute.SetInt(id, val); }
        private void PushVector(int id, Vector3 val) { if (SwarmCompute != null) SwarmCompute.SetVector(id, val); }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying || SwarmCompute == null) return;
            PushAllComputeData();
        }
#endif

        private void PushAllComputeData()
        {
            PushFloat(SpeedID, speed);
            PushFloat(SightRadiusID, sightRadius);
            PushFloat(SeparationWeightID, separationWeight);
            PushFloat(AlignmentWeightID, alignmentWeight);
            PushFloat(CohesionWeightID, cohesionWeight);
            PushFloat(FloorYID, floorY);
            PushFloat(AvoidanceMarginID, avoidanceMargin);
            PushFloat(PredatorFleeWeightID, predatorFleeWeight);
            PushFloat(TubeRadiusID, tubeRadius);
            PushFloat(SingularitySoftenID, singularitySoften);
            PushFloat(ArrivalMinSpeedID, arrivalMinSpeed);
            PushFloat(ArrivalRadiusSqID, arrivalRadiusSq);
            PushFloat(SwirlStrengthID, swirlStrength);
            PushFloat(TargetWeightID, targetWeight);
            PushVector(TargetPositionID, targetPosition);
            PushFloat(CellSizeID, cellSize);
            PushInt(GridSizeID, gridSize);
        }

        public void RegisterAttractor(BoidAttractor a) { if (!activeAttractors.Contains(a)) activeAttractors.Add(a); }
        public void UnregisterAttractor(BoidAttractor a) => activeAttractors.Remove(a);
        public void RegisterPredator(BoidPredator p) { if (!activePredators.Contains(p)) activePredators.Add(p); }
        public void UnregisterPredator(BoidPredator p) => activePredators.Remove(p);
        public void RegisterObstacle(BoidObstacle o) { if (!activeObstacles.Contains(o)) activeObstacles.Add(o); }
        public void UnregisterObstacle(BoidObstacle o) => activeObstacles.Remove(o);
        public void SetTargetPosition(Vector3 newTarget) => TargetPosition = newTarget;

        public void SyncEnvironmentData()
        {
            int aCount = CurrentAttractorCount;
            if (aCount > 0)
            {
                for (int i = 0; i < aCount; i++)
                {
                    Vector3 currentPos = activeAttractors[i].transform.position;
                    Vector3 velocity = (currentPos - previousAttractorPositions[i]) / Time.deltaTime;
                    previousAttractorPositions[i] = currentPos;

                    attractorDataCache[i] = new AttractorData
                    {
                        position = currentPos,
                        weight = activeAttractors[i].weight,
                        velocity = velocity,
                        padding = 0f
                    };
                }
                attractorsBuffer.SetData(attractorDataCache, 0, 0, aCount);
            }

            int pCount = CurrentPredatorCount;
            if (pCount > 0)
            {
                for (int i = 0; i < pCount; i++)
                {
                    predatorDataCache[i] = new PredatorData
                    {
                        position = activePredators[i].transform.position,
                        radiusSq = activePredators[i].panicRadius * activePredators[i].panicRadius
                    };
                }
                predatorsBuffer.SetData(predatorDataCache, 0, 0, pCount);
            }

            int oCount = CurrentObstacleCount;
            if (oCount > 0)
            {
                for (int i = 0; i < oCount; i++)
                {
                    Quaternion q = activeObstacles[i].transform.rotation;
                    obstacleDataCache[i] = new ObstacleData
                    {
                        position = activeObstacles[i].transform.position,
                        type = (int)activeObstacles[i].shapeType,
                        extents = activeObstacles[i].extents,
                        padding = 0f,
                        rotation = new Vector4(q.x, q.y, q.z, q.w)
                    };
                }
                obstaclesBuffer.SetData(obstacleDataCache, 0, 0, oCount);
            }
        }

        private void PopulateInitialData()
        {
            Boid[] boids = new Boid[boidCount];
            for (int i = 0; i < boidCount; i++)
            {
                const ushort roll16 = (ushort)(0.5f * 65535f); 
                ushort id16 = (ushort)i;
                uint packed = ((uint)id16 << 16) | roll16;

                boids[i] = new Boid
                {
                    position = transform.position + Random.insideUnitSphere * 10f,
                    randomSeed = Random.value, 
                    velocity = Random.onUnitSphere,
                    colorSeed = Random.value,  
                    packedData = packed, 
                    splineT = Random.value,
                    pad1 = 0f, pad2 = 0f
                };
            }
            readBuffer.SetData(boids);
            writeBuffer.SetData(boids);
        }

        public void PingPongBuffers() => (writeBuffer, readBuffer) = (readBuffer, writeBuffer);

        public void ReleaseAllBuffers()
        {
            readBuffer?.Release(); writeBuffer?.Release(); sortBuffer?.Release();
            gridOffsets?.Release(); splineBuffer?.Release(); argsBuffer?.Release();
            attractorsBuffer?.Release(); predatorsBuffer?.Release(); obstaclesBuffer?.Release();
            tempSortBuffer?.Release(); globalHistBuffer?.Release(); localOffsetsBuffer?.Release();
            visibleBoidsBuffer?.Release();
    
            readBuffer = null; writeBuffer = null; sortBuffer = null;
            gridOffsets = null; splineBuffer = null; argsBuffer = null;
            attractorsBuffer = null; predatorsBuffer = null; obstaclesBuffer = null;
            tempSortBuffer = null; globalHistBuffer = null; localOffsetsBuffer = null;
            visibleBoidsBuffer = null;
        }

        private void OnDestroy()
        {
            ReleaseAllBuffers();
            if (SwarmCompute != null) Destroy(SwarmCompute);
        }
    }
}