using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace TechArtPlayground
{
    [DefaultExecutionOrder(-50)]
    public class BoidsManager : MonoBehaviour
    {
        private const float OBSTACLE_SCAN_RATE = 10f;

        // --- SHADER PROPERTY IDs ---
        // Storing IDs as ints is O(1) for the GPU, whereas passing strings is O(N) allocation per frame.
        private static readonly int CellSize = Shader.PropertyToID("cellSize");
        private static readonly int GridSize = Shader.PropertyToID("gridSize");
        private static readonly int DeltaTime = Shader.PropertyToID("deltaTime");
        private static readonly int Time1 = Shader.PropertyToID("time");
        private static readonly int Speed = Shader.PropertyToID("speed");
        private static readonly int NumBoids = Shader.PropertyToID("numBoids");
        private static readonly int PaddedCount = Shader.PropertyToID("paddedCount");
        private static readonly int SightRadius = Shader.PropertyToID("sightRadius");
        private static readonly int SeparationWeight = Shader.PropertyToID("separationWeight");
        private static readonly int TargetPosition = Shader.PropertyToID("targetPosition");
        private static readonly int TargetWeight = Shader.PropertyToID("targetWeight");
        private static readonly int FloorY = Shader.PropertyToID("floorY");
        private static readonly int AvoidanceMargin = Shader.PropertyToID("avoidanceMargin");
        private static readonly int NumObstacles = Shader.PropertyToID("numObstacles");
        private static readonly int NumPredators = Shader.PropertyToID("numPredators");
        private static readonly int BoidsBuffer = Shader.PropertyToID("boidsBuffer");
        private static readonly int ReadBoidsBuffer = Shader.PropertyToID("ReadBoidsBuffer");
        private static readonly int WriteBoidsBuffer = Shader.PropertyToID("WriteBoidsBuffer");
        private static readonly int NumAttractors = Shader.PropertyToID("numAttractors");
        private static readonly int AttractorsBuffer = Shader.PropertyToID("attractorsBuffer");
        private static readonly int SwirlStrength = Shader.PropertyToID("swirlStrength");
        private static readonly int ArrivalRadiusSq = Shader.PropertyToID("arrivalRadiusSq");
        private static readonly int ArrivalMinSpeed = Shader.PropertyToID("arrivalMinSpeed");
        private static readonly int SingularitySoften = Shader.PropertyToID("singularitySoften");
        private static readonly int TubeRadius = Shader.PropertyToID("tubeRadius");
        private static readonly int PredatorFleeWeight = Shader.PropertyToID("predatorFleeWeight");

        [Header("Optimization")] [Range(1, 10)]
        public int sortFrequency = 4;

        [Header("Predator Settings")] public int maxPredators = 16;

        [Header("References")] public ComputeShader boidsCompute;

        public ComputeShader bitonicSortCompute;
        public Mesh fishMesh;
        public Material fishMaterial;

        [Header("Banc Parameters")] public int numBoids = 10000;

        public float spawnRadius = 40f;
        public float speed = 5f;
        public float sightRadius = 3f;
        public float separationWeight = 1.5f;
        public float targetWeight = 0.5f;

        [Header("Spatial Grid")] public float cellSize = 3f;

        public int gridSize = 64;

        [Header("Variations Organiques")] public float separationPulseSpeed = 1.0f;

        public float separationPulseAmount = 0.8f;

        [Header("Visuals")] public Color colorA = Color.white;

        public Color colorB = Color.blue;
        public float minSize = 0.5f;
        public float maxSize = 1.5f;
        public float predatorRadius = 5f;

        [Header("World Limits")] public float floorY;

        public float floorAvoidanceMargin = 2f;
        public LayerMask obstacleLayer;
        public int maxObstacles = 50;
        public float scanRadius = 50f;

        [Header("Simulation Mode")] public bool useSplineFlow;

        [Header("Spline Flow Settings")] public float tubeRadius = 5f;

        public float predatorFleeWeight = 20f;


        [Header("Attractor Tuning")] public float globalAttractorWeight = 1.0f;

        [Tooltip("How much the boids swirl around the attractor (0 = direct impact, 1 = perfect orbit)")]
        [Range(0f, 1f)]
        public float swirlStrength = 0.85f;

        [Tooltip("The radius at which boids begin to slow down and swirl")]
        public float arrivalRadius = 5.0f;

        [Tooltip("The minimum speed multiplier when exactly at the center of an attractor")] [Range(0.1f, 1f)]
        public float arrivalMinSpeed = 0.3f;

        [Tooltip("Prevents infinite mathematical weight when boids hit the exact center. Higher = softer pull.")]
        public float singularitySoften = 2.0f;

        [SerializeField] private SplineContainer splineContainer;
        private readonly List<BoidAttractor> activeAttractors = new();

        // Component Registries (Observer Pattern)
        private readonly List<BoidPredator> activePredators = new();

        // Caching & Pooling Arrays
        private readonly Collider[] collBuffer = new Collider[32];
        private readonly int maxAttractors = 20;

        // Graphics Buffers
        private GraphicsBuffer argsBuffer;
        private AttractorData[] attractorDataArray;
        private GraphicsBuffer attractorsBuffer;
        private GraphicsBuffer boidsBufferA;
        private GraphicsBuffer boidsBufferB;

        // Kernel IDs
        private int clearGridKernel,
            populateHashesKernel,
            buildOffsetsKernel,
            csMainKernel,
            reorderBoidsKernel,
            splineFlowKernel;

        private GraphicsBuffer currentBuffer;

        private int frameCount;
        private GraphicsBuffer gridOffsetsBuffer;

        // State Tracking (Dirty Flag Pattern)
        private int lastAttractorCount = -1;
        private int lastPredatorCount = -1;
        private GraphicsBuffer nextBuffer;
        private int obstacleCount;
        private Obstacle[] obstaclesArray;
        private GraphicsBuffer obstaclesBuffer;
        private float obstacleScanTimer;
        private int paddedCount;
        private PredatorData[] predatorDataArray;
        private GraphicsBuffer predatorsBuffer;
        private Vector3[] previousAttractorPositions; // Kinematics cache for velocity calculation
        private GraphicsBuffer sortBuffer;
        private GraphicsBuffer splineBuffer;
        private GraphicsBuffer splineTBuffer;

        // --- INTERNAL ARCHITECTURE ---
        public static BoidsManager Instance { get; private set; }

        private void Awake()
        {
            // Standard Singleton implementation
            if (Instance != null && Instance != this) Destroy(this);
            else Instance = this;
        }

        private void Start()
        {
            // --- 0. KERNEL SETUP ---
            clearGridKernel = boidsCompute.FindKernel("ClearGrid");
            populateHashesKernel = boidsCompute.FindKernel("PopulateHashes");
            buildOffsetsKernel = boidsCompute.FindKernel("BuildGridOffsets");
            csMainKernel = boidsCompute.FindKernel("CSMain");
            reorderBoidsKernel = boidsCompute.FindKernel("ReorderBoids");
            splineFlowKernel = boidsCompute.FindKernel("CSMain_SplineFlow");

            // Bitonic Sort requires arrays strictly sized in Powers of Two
            paddedCount = Mathf.NextPowerOfTwo(numBoids);

            // --- 1. INIT BOIDS (Core Data) ---
            // Inside Start():
            Boid[] boidsArray = new Boid[numBoids];
            for (int i = 0; i < numBoids; i++)
            {
                boidsArray[i].position = transform.position + Random.insideUnitSphere * spawnRadius;
                boidsArray[i].velocity = Random.onUnitSphere * speed;
                // boidsArray[i].roll = 0f;

                // THE FIX: Pack the persistent ID (i) into the integer portion of the float.
                // The fractional part (Random.value) remains the path progress!
                boidsArray[i].splineT = i + Random.value;
            }

            // OPTIMIZATION: Stride is now exactly 32 bytes (4 floats * 8)
            int boidStride = 32;
            boidsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numBoids, boidStride);
            boidsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numBoids, boidStride);
            boidsBufferA.SetData(boidsArray);
            boidsBufferB.SetData(boidsArray);

            // --- 2. OPTIONAL SPLINE DATA (SoA Pattern) ---
            if (useSplineFlow)
            {
                float[] initialSplineT = new float[numBoids];
                for (int i = 0; i < numBoids; i++) initialSplineT[i] = Random.value; // 0.0 to 1.0

                // Stride is just 4 bytes (sizeof(float))
                splineTBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numBoids, sizeof(float));
                splineTBuffer.SetData(initialSplineT);

                // Bind immediately to the spline kernel
                boidsCompute.SetBuffer(splineFlowKernel, "SplineTBuffer", splineTBuffer);
            }

            // --- 3. INIT ATTRACTORS (32-byte stride alignment) ---
            attractorDataArray = new AttractorData[maxAttractors];
            previousAttractorPositions = new Vector3[maxAttractors];
            attractorsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxAttractors, 32);

            // --- 4. INIT SPATIAL GRID ---
            int totalCells = gridSize * gridSize * gridSize;
            gridOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalCells, sizeof(int));
            sortBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, paddedCount, sizeof(uint) * 2);

            // --- 5. INIT OBSTACLES & PREDATORS ---
            obstaclesArray = new Obstacle[maxObstacles];
            obstaclesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxObstacles, 16);
            predatorDataArray = new PredatorData[maxPredators];
            predatorsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxPredators, 16);

            // --- 6. SPLINE BAKING ---
            int resolution = 100; // O(K) space complexity
            SplineSampleData[] bakedSpline = new SplineSampleData[resolution];
            float length = splineContainer.CalculateLength();

            for (int i = 0; i < resolution; i++)
            {
                float t = i / (float)(resolution - 1);
                bakedSpline[i].position = splineContainer.EvaluatePosition(t);
                bakedSpline[i].tangent = ((Vector3)splineContainer.EvaluateTangent(t)).normalized;
                bakedSpline[i].width = tubeRadius; // River width
            }

            splineBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, resolution, 28);
            splineBuffer.SetData(bakedSpline);

            boidsCompute.SetInt("splineResolution", resolution);
            boidsCompute.SetFloat("splineLength", length);
            boidsCompute.SetBuffer(splineFlowKernel, "splineBuffer", splineBuffer);

            // --- 7. BINDING BUFFERS TO KERNELS ---
            // Grid Setup
            boidsCompute.SetBuffer(clearGridKernel, "gridOffsets", gridOffsetsBuffer);
            boidsCompute.SetBuffer(populateHashesKernel, "SortBuffer", sortBuffer);
            boidsCompute.SetBuffer(buildOffsetsKernel, "SortBuffer", sortBuffer);
            boidsCompute.SetBuffer(buildOffsetsKernel, "gridOffsets", gridOffsetsBuffer);
            boidsCompute.SetBuffer(reorderBoidsKernel, "SortBuffer", sortBuffer);

            // Physics passes
            int[] physicsKernels = { csMainKernel, splineFlowKernel };
            foreach (int k in physicsKernels)
            {
                boidsCompute.SetBuffer(k, "gridOffsets", gridOffsetsBuffer);
                boidsCompute.SetBuffer(k, "SortBuffer", sortBuffer);
                boidsCompute.SetBuffer(k, "obstaclesBuffer", obstaclesBuffer);
                boidsCompute.SetBuffer(k, "predatorsBuffer", predatorsBuffer);
                boidsCompute.SetBuffer(k, "attractorsBuffer", attractorsBuffer);
            }

            predatorsBuffer.SetData(predatorDataArray, 0, 0, activePredators.Count);
            boidsCompute.SetInt(NumPredators, activePredators.Count);
            lastPredatorCount = activePredators.Count;

            // --- 8. SETUP DRAW ARGS ---
            uint[] args = new uint[5] { fishMesh.GetIndexCount(0), (uint)numBoids, 0, 0, 0 };
            argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, args.Length * sizeof(uint));
            argsBuffer.SetData(args);

            // Push static properties exactly once to save PCIe bus bandwidth
            PushStaticGPUProperties();

            boidsCompute.SetInt(NumBoids, numBoids);
            boidsCompute.SetInt(PaddedCount, paddedCount);

            UpdateObstacles();

            currentBuffer = boidsBufferA;
            nextBuffer = boidsBufferB;
        }

        private void Update()
        {
            frameCount++;

            // --- 1. OBSTACLE SCAN ---
            obstacleScanTimer += Time.deltaTime;
            if (obstacleScanTimer >= OBSTACLE_SCAN_RATE)
            {
                obstacleScanTimer = 0f;
                UpdateObstacles();
            }

            // --- 2. DYNAMIC GLOBALS ---
            boidsCompute.SetFloat(DeltaTime, Time.deltaTime);
            boidsCompute.SetFloat(Time1, Time.time);

            float dynamicSeparation = Mathf.Max(0.1f,
                separationWeight + Mathf.Sin(Time.time * separationPulseSpeed) * separationPulseAmount);
            boidsCompute.SetFloat(SeparationWeight, dynamicSeparation);

            if (transform.hasChanged)
            {
                boidsCompute.SetVector(TargetPosition, transform.position);
                transform.hasChanged = false;
            }

            // --- 3. PREDATORS UPDATE (Dirty Flag Optimization) ---
            int currentPredatorCount = activePredators.Count;
            bool predatorsDirty = currentPredatorCount != lastPredatorCount;

            for (int i = 0; i < currentPredatorCount; i++)
            {
                Transform predTransform = activePredators[i].transform;
                if (predTransform.hasChanged)
                {
                    predatorsDirty = true;
                    predatorDataArray[i].position = predTransform.position;
                    // Do not reset hasChanged in case other scripts rely on it
                }

                predatorDataArray[i].radiusSq = Mathf.Pow(activePredators[i].panicRadius, 2);
            }

            if (predatorsDirty)
            {
                predatorsBuffer.SetData(predatorDataArray, 0, 0, currentPredatorCount);
                boidsCompute.SetInt(NumPredators, currentPredatorCount);
                lastPredatorCount = currentPredatorCount;
            }

            // --- 4. ATTRACTORS UPDATE (Dirty Flag Optimization) ---
            int currentAttractorCount = Mathf.Min(activeAttractors.Count, maxAttractors);
            bool attractorsDirty = currentAttractorCount != lastAttractorCount;

            for (int i = 0; i < currentAttractorCount; i++)
            {
                BoidAttractor attr = activeAttractors[i];
                Transform attrTransform = attr.transform;
                Vector3 currentPos = attrTransform.position;

                // Update if moving OR if velocity hasn't settled to 0 yet
                if (attrTransform.hasChanged || attractorDataArray[i].velocity.sqrMagnitude > 0.0001f)
                {
                    attractorsDirty = true;
                    Vector3 velocity = (currentPos - previousAttractorPositions[i]) / Time.deltaTime;
                    previousAttractorPositions[i] = currentPos;

                    attractorDataArray[i].position = currentPos;
                    attractorDataArray[i].velocity = velocity;
                    attrTransform.hasChanged = false; // Reset to catch the next movement
                }

                attractorDataArray[i].weight = attr.weight * globalAttractorWeight;
                attractorDataArray[i].padding = 0f;
            }

            if (attractorsDirty && currentAttractorCount > 0)
            {
                attractorsBuffer.SetData(attractorDataArray, 0, 0, currentAttractorCount);
                boidsCompute.SetInt(NumAttractors, currentAttractorCount);
                lastAttractorCount = currentAttractorCount;
            }

            // Thread Group calculations
            int totalCells = gridSize * gridSize * gridSize;
            int gridThreadGroups = (totalCells + 63) / 64;
            int boidThreadGroups = (numBoids + 63) / 64;
            int paddedThreadGroups = (paddedCount + 63) / 64;

            // --- 5. THE SORTING PASS (Runs every N frames) ---
            if (frameCount % sortFrequency == 0)
            {
                boidsCompute.Dispatch(clearGridKernel, gridThreadGroups, 1, 1);

                boidsCompute.SetBuffer(populateHashesKernel, ReadBoidsBuffer, currentBuffer);
                boidsCompute.Dispatch(populateHashesKernel, paddedThreadGroups, 1, 1);

                // GPUSort.Sort(bitonicSortCompute, sortBuffer, paddedCount);

                boidsCompute.SetBuffer(reorderBoidsKernel, ReadBoidsBuffer, currentBuffer);
                boidsCompute.SetBuffer(reorderBoidsKernel, WriteBoidsBuffer, nextBuffer);
                boidsCompute.Dispatch(reorderBoidsKernel, boidThreadGroups, 1, 1);

                boidsCompute.Dispatch(buildOffsetsKernel, boidThreadGroups, 1, 1);

                // Swap pointers so CSMain reads the sorted buffer
                (currentBuffer, nextBuffer) = (nextBuffer, currentBuffer);
            }

            // --- 6. THE PHYSICS PASS (Runs EVERY frame) ---
            int activeKernel = useSplineFlow ? splineFlowKernel : csMainKernel;

            boidsCompute.SetBuffer(activeKernel, ReadBoidsBuffer, currentBuffer);
            boidsCompute.SetBuffer(activeKernel, WriteBoidsBuffer, nextBuffer);
            boidsCompute.Dispatch(activeKernel, boidThreadGroups, 1, 1);

            // --- 7. PREPARE FOR RENDER ---
            (currentBuffer, nextBuffer) = (nextBuffer, currentBuffer);

            fishMaterial.SetBuffer(BoidsBuffer, currentBuffer);
            Graphics.DrawMeshInstancedIndirect(fishMesh, 0, fishMaterial,
                new Bounds(transform.position, Vector3.one * 1000f), argsBuffer);
        }

        private void OnDestroy()
        {
            boidsBufferA?.Release();
            boidsBufferB?.Release();
            argsBuffer?.Release();
            obstaclesBuffer?.Release();
            gridOffsetsBuffer?.Release();
            sortBuffer?.Release();
            predatorsBuffer?.Release();
            attractorsBuffer?.Release();
            splineTBuffer?.Release();
            splineBuffer?.Release();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Allows real-time inspector editing without pushing data every single frame
            if (Application.isPlaying) PushStaticGPUProperties();
        }
#endif

        /// <summary>
        ///     Pushes variables that rarely change to the GPU.
        ///     Called on Start and whenever Inspector values change.
        /// </summary>
        public void PushStaticGPUProperties()
        {
            if (boidsCompute is null) return;

            boidsCompute.SetFloat(Speed, speed);
            boidsCompute.SetFloat(TargetWeight, targetWeight);
            boidsCompute.SetFloat(SwirlStrength, swirlStrength);
            boidsCompute.SetFloat(ArrivalRadiusSq, arrivalRadius * arrivalRadius);
            boidsCompute.SetFloat(ArrivalMinSpeed, arrivalMinSpeed);
            boidsCompute.SetFloat(SingularitySoften, singularitySoften);

            boidsCompute.SetFloat(CellSize, cellSize);
            boidsCompute.SetInt(GridSize, gridSize);
            boidsCompute.SetFloat(SightRadius, sightRadius);
            boidsCompute.SetFloat(FloorY, floorY);
            boidsCompute.SetFloat(AvoidanceMargin, floorAvoidanceMargin);


            boidsCompute.SetFloat(TubeRadius, tubeRadius);
            boidsCompute.SetFloat(PredatorFleeWeight, predatorFleeWeight);
        }

        // --- REGISTRY METHODS ---
        public void RegisterPredator(BoidPredator predator)
        {
            if (!activePredators.Contains(predator) && activePredators.Count < maxPredators)
                activePredators.Add(predator);
        }

        public void UnregisterPredator(BoidPredator predator)
        {
            activePredators.Remove(predator);
        }

        public void RegisterAttractor(BoidAttractor attractor)
        {
            if (!activeAttractors.Contains(attractor) && activeAttractors.Count < maxAttractors)
                activeAttractors.Add(attractor);
        }

        public void UnregisterAttractor(BoidAttractor attractor)
        {
            activeAttractors.Remove(attractor);
        }

        private void UpdateObstacles()
        {
            int size = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, collBuffer, obstacleLayer);
            obstacleCount = Mathf.Min(size, maxObstacles);

            for (int i = 0; i < obstacleCount; i++)
            {
                obstaclesArray[i].position = collBuffer[i].bounds.center;
                obstaclesArray[i].radiusSq = Mathf.Pow(collBuffer[i].bounds.extents.magnitude + 1.0f, 2);
            }

            obstaclesBuffer.SetData(obstaclesArray);
            boidsCompute.SetInt(NumObstacles, obstacleCount);
        }

        // --- DATA STRUCTURES ---
        private struct AttractorData
        {
            public Vector3 position;
            public float weight;
            public Vector3 velocity;
            public float padding; // Critical for 32-byte GPU alignment
        }

        private struct PredatorData
        {
            public Vector3 position;
            public float radiusSq;
        }

        private struct Obstacle
        {
            public Vector3 position;
            public float radiusSq;
        }

        public struct SplineSampleData
        {
            public Vector3 position;
            public Vector3 tangent;
            public float width; // Optional: allows the current to widen or narrow
        }

        struct Boid 
        { 
            public Vector3 position; 
            public float randomSeed; 
            public Vector3 velocity; 
            public float colorSeed; 
            public uint packedData; 
            public float splineT; 
            public float pad1; 
            public float pad2; 
        };
    }
}