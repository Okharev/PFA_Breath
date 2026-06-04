using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Splines;
using R3;
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
        // Compute Shader Cache IDs mapped strictly for this local Swarm
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
        public Bounds swarmBounds = new(Vector3.zero, new Vector3(100, 100, 100));
        public bool followSpline;

        [Header("Dynamic Environment Limits")] 
        public int maxAttractors = 16;
        public int maxPredators = 16;
        public int maxObstacles = 32;

        [Header("Spline Settings")] 
        public SplineContainer splineContainer;
        public int splineResolution = 100;
        [Range(0.1f, 10f)] [SerializeField] private float tubeRadius = 2.0f;
        public float splineLength { get; private set; }

        [Header("Flocking Behaviors")] 
        [Range(0.1f, 20f)] [SerializeField] private float speed = 4.0f;
        [Range(0.1f, 10f)] [SerializeField] private float sightRadius = 2.5f;
        [Range(0.0f, 5f)] [SerializeField] private float separationWeight = 1.5f;
        [Range(0.0f, 5f)] [SerializeField] private float alignmentWeight = 1.0f;
        [Range(0.0f, 5f)] [SerializeField] private float cohesionWeight = 1.5f;

        [Header("Environment & Avoidance")] 
        [SerializeField] private float floorY = -10f;
        [SerializeField] private float avoidanceMargin = 2.0f;
        [Range(0f, 10f)] [SerializeField] private float predatorFleeWeight = 5.0f;

        [Header("Attractors & Waypoints")] 
        [SerializeField] private Vector3 defaultWaypoint = Vector3.zero;
        [Range(0f, 5f)] [SerializeField] private float targetWeight = 1.0f;
        [SerializeField] private float swirlStrength = 2.0f;
        [SerializeField] private float arrivalRadiusSq = 25.0f; 
        [SerializeField] private float arrivalMinSpeed = 0.5f;
        [SerializeField] private float singularitySoften = 1.0f;

        [Header("Optimization")]
        [Range(1, 10)] public int sortFrequency = 4;

        [Header("Spatial Grid Tuning")] 
        [SerializeField] public float cellSize = 3.0f;
        [SerializeField] public int gridSize = 64;

        // --- R3 BACK-END ---
        private readonly ReactiveProperty<float> _speedRx = new();
        private readonly ReactiveProperty<float> _sightRadiusRx = new();
        private readonly ReactiveProperty<float> _separationRx = new();
        private readonly ReactiveProperty<float> _alignmentRx = new();
        private readonly ReactiveProperty<float> _cohesionRx = new();
        private readonly ReactiveProperty<float> _floorYRx = new();
        private readonly ReactiveProperty<float> _avoidanceRx = new();
        private readonly ReactiveProperty<float> _predatorFleeRx = new();
        private readonly ReactiveProperty<float> _tubeRadiusRx = new();
        private readonly ReactiveProperty<float> _singularityRx = new();
        private readonly ReactiveProperty<float> _arrivalMinSpeedRx = new();
        private readonly ReactiveProperty<float> _arrivalRadiusSqRx = new();
        private readonly ReactiveProperty<float> _swirlStrengthRx = new();
        private readonly ReactiveProperty<float> _targetWeightRx = new();
        private readonly ReactiveProperty<Vector3> _targetPositionRx = new();
        private readonly ReactiveProperty<float> _cellSizeRx = new();
        private readonly ReactiveProperty<int> _gridSizeRx = new();

        private DisposableBag _disposables;

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
            _disposables = new DisposableBag();

            // 1. ISOLATE THE COMPUTE SHADER
            SwarmCompute = Instantiate(baseCompute);
            SwarmCompute.name = $"BoidsCompute_{gameObject.name}";

            defaultWaypoint = transform.position;
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
            InitializeReactivePipelines();
            ForceUpdateReactiveState();
        }

        private void InitializeReactivePipelines()
        {
            _speedRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(SpeedID, v)).AddTo(ref _disposables);
            _sightRadiusRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(SightRadiusID, v)).AddTo(ref _disposables);
            _separationRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(SeparationWeightID, v)).AddTo(ref _disposables);
            _alignmentRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(AlignmentWeightID, v)).AddTo(ref _disposables);
            _cohesionRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(CohesionWeightID, v)).AddTo(ref _disposables);
            _floorYRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(FloorYID, v)).AddTo(ref _disposables);
            _avoidanceRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(AvoidanceMarginID, v)).AddTo(ref _disposables);
            _predatorFleeRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(PredatorFleeWeightID, v)).AddTo(ref _disposables);
            _tubeRadiusRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(TubeRadiusID, v)).AddTo(ref _disposables);
            _singularityRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(SingularitySoftenID, v)).AddTo(ref _disposables);
            _arrivalMinSpeedRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(ArrivalMinSpeedID, v)).AddTo(ref _disposables);
            _arrivalRadiusSqRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(ArrivalRadiusSqID, v)).AddTo(ref _disposables);
            _swirlStrengthRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(SwirlStrengthID, v)).AddTo(ref _disposables);
            _targetWeightRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(TargetWeightID, v)).AddTo(ref _disposables);
            _targetPositionRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetVector(TargetPositionID, v)).AddTo(ref _disposables);
            _cellSizeRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetFloat(CellSizeID, v)).AddTo(ref _disposables);
            _gridSizeRx.DistinctUntilChanged().Subscribe(this, (v, s) => s.SwarmCompute.SetInt(GridSizeID, v)).AddTo(ref _disposables);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying || SwarmCompute == null) return;
            ForceUpdateReactiveState();
        }
#endif

        private void ForceUpdateReactiveState()
        {
            _speedRx.Value = speed;
            _sightRadiusRx.Value = sightRadius;
            _separationRx.Value = separationWeight;
            _alignmentRx.Value = alignmentWeight;
            _cohesionRx.Value = cohesionWeight;
            _floorYRx.Value = floorY;
            _avoidanceRx.Value = avoidanceMargin;
            _predatorFleeRx.Value = predatorFleeWeight;
            _tubeRadiusRx.Value = tubeRadius;
            _singularityRx.Value = singularitySoften;
            _arrivalMinSpeedRx.Value = arrivalMinSpeed;
            _arrivalRadiusSqRx.Value = arrivalRadiusSq;
            _swirlStrengthRx.Value = swirlStrength;
            _targetWeightRx.Value = targetWeight;
            _targetPositionRx.Value = defaultWaypoint;
            _cellSizeRx.Value = cellSize;
            _gridSizeRx.Value = gridSize;
        }

        public void RegisterAttractor(BoidAttractor a) { if (!activeAttractors.Contains(a)) activeAttractors.Add(a); }
        public void UnregisterAttractor(BoidAttractor a) => activeAttractors.Remove(a);
        public void RegisterPredator(BoidPredator p) { if (!activePredators.Contains(p)) activePredators.Add(p); }
        public void UnregisterPredator(BoidPredator p) => activePredators.Remove(p);
        public void RegisterObstacle(BoidObstacle o) { if (!activeObstacles.Contains(o)) activeObstacles.Add(o); }
        public void UnregisterObstacle(BoidObstacle o) => activeObstacles.Remove(o);
        public void SetTargetPosition(Vector3 newTarget) => _targetPositionRx.Value = newTarget;

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
                ushort roll16 = (ushort)(0.5f * 65535f); 
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
            _disposables.Dispose();
            ReleaseAllBuffers();
            if (SwarmCompute != null) Destroy(SwarmCompute);
        }
    }
}