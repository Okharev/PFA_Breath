using System.Collections.Generic;
using UnityEngine;

namespace TechArtPlayground
{
    [DefaultExecutionOrder(-50)]
    public class BoidsManager : MonoBehaviour
    {
        private const float OBSTACLE_SCAN_RATE = 10f;

        // Property IDs
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

        [Header("Optimization")] [Range(1, 10)]
        public int sortFrequency = 4;

        [Header("Predator Settings")] public int maxPredators = 16;

        [Header("References")] public ComputeShader boidsCompute;

        public ComputeShader bitonicSortCompute;
        public Mesh fishMesh;
        public Material fishMaterial;
        public Transform playerTransform;

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
        private readonly List<BoidPredator> activePredators = new();

        private GraphicsBuffer argsBuffer;

        // PING PONG BUFFERS (Modern Unity 6.4 GraphicsBuffers)
        private GraphicsBuffer boidsBufferA;
        private GraphicsBuffer boidsBufferB;

        private int clearGridKernel, populateHashesKernel, buildOffsetsKernel, csMainKernel, reorderBoidsKernel;
        private readonly Collider[] collBuffer = new Collider[32];

        private GraphicsBuffer currentBuffer;
        private int frameCount;
        private GraphicsBuffer gridOffsetsBuffer;
        private GraphicsBuffer nextBuffer;
        private int obstacleCount;

        private Obstacle[] obstaclesArray;
        private GraphicsBuffer obstaclesBuffer;

        private float obstacleScanTimer;
        private int paddedCount;
        private PredatorData[] predatorDataArray;
        private GraphicsBuffer predatorsBuffer;
        private GraphicsBuffer sortBuffer;
        public static BoidsManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) Destroy(this);
            else Instance = this;
        }

        private void Start()
        {
            clearGridKernel = boidsCompute.FindKernel("ClearGrid");
            populateHashesKernel = boidsCompute.FindKernel("PopulateHashes");
            buildOffsetsKernel = boidsCompute.FindKernel("BuildGridOffsets");
            csMainKernel = boidsCompute.FindKernel("CSMain");

            // NOUVEAU: Find the reorder kernel
            reorderBoidsKernel = boidsCompute.FindKernel("ReorderBoids");

            paddedCount = Mathf.NextPowerOfTwo(numBoids);

            // 1. Init Boids
            Boid[] boidsArray = new Boid[numBoids];
            for (int i = 0; i < numBoids; i++)
            {
                boidsArray[i].position = transform.position + Random.insideUnitSphere * spawnRadius;
                boidsArray[i].direction = Random.onUnitSphere;
                Color randomColor = Color.Lerp(colorA, colorB, Random.value);
                boidsArray[i].color = new Vector3(randomColor.r, randomColor.g, randomColor.b);
                boidsArray[i].size = Random.Range(minSize, maxSize);
                boidsArray[i].currentSpeed = speed;
                boidsArray[i].roll = 0f;
                boidsArray[i].flapPhase = Random.Range(0f, 100f);
            }

            // Allocate Two Buffers
            int boidStride = 52;
            boidsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numBoids, boidStride);
            boidsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numBoids, boidStride);

            boidsBufferA.SetData(boidsArray);
            boidsBufferB.SetData(boidsArray); // Initialize both safely

            // 2. Spatial Grid Buffers
            int totalCells = gridSize * gridSize * gridSize;
            gridOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalCells, sizeof(int));
            sortBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, paddedCount, sizeof(uint) * 2);

            // 3. Obstacle & Predator Buffers
            obstaclesArray = new Obstacle[maxObstacles];
            obstaclesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxObstacles, 16);
            predatorDataArray = new PredatorData[maxPredators];
            predatorsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxPredators, 16);

            // 4. Static Bindings (Buffers that don't swap)
            boidsCompute.SetBuffer(clearGridKernel, "gridOffsets", gridOffsetsBuffer);
            boidsCompute.SetBuffer(populateHashesKernel, "SortBuffer", sortBuffer);
            boidsCompute.SetBuffer(buildOffsetsKernel, "SortBuffer", sortBuffer);
            boidsCompute.SetBuffer(buildOffsetsKernel, "gridOffsets", gridOffsetsBuffer);
            boidsCompute.SetBuffer(csMainKernel, "gridOffsets", gridOffsetsBuffer);
            boidsCompute.SetBuffer(csMainKernel, "SortBuffer", sortBuffer);
            boidsCompute.SetBuffer(csMainKernel, "obstaclesBuffer", obstaclesBuffer);
            boidsCompute.SetBuffer(csMainKernel, "predatorsBuffer", predatorsBuffer);
            boidsCompute.SetBuffer(reorderBoidsKernel, "SortBuffer", sortBuffer);

            // 5. Args Buffer
            uint[] args = new uint[5] { fishMesh.GetIndexCount(0), (uint)numBoids, 0, 0, 0 };
            argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, args.Length * sizeof(uint));
            argsBuffer.SetData(args);

            UpdateObstacles();

            currentBuffer = boidsBufferA;
            nextBuffer = boidsBufferB;
        }

        private void Update()
        {
            frameCount++;

            obstacleScanTimer += Time.deltaTime;
            if (obstacleScanTimer >= OBSTACLE_SCAN_RATE)
            {
                obstacleScanTimer = 0f;
                UpdateObstacles();
            }

            // --- 1. SET GLOBALS ---
            float dynamicSeparation = Mathf.Max(0.1f,
                separationWeight + Mathf.Sin(Time.time * separationPulseSpeed) * separationPulseAmount);
            boidsCompute.SetFloat(CellSize, cellSize);
            boidsCompute.SetInt(GridSize, gridSize);
            boidsCompute.SetFloat(DeltaTime, Time.deltaTime);
            boidsCompute.SetFloat(Time1, Time.time);
            boidsCompute.SetFloat(Speed, speed);
            boidsCompute.SetInt(NumBoids, numBoids);
            boidsCompute.SetInt(PaddedCount, paddedCount);
            boidsCompute.SetFloat(SightRadius, sightRadius);
            boidsCompute.SetFloat(SeparationWeight, dynamicSeparation);
            boidsCompute.SetVector(TargetPosition, transform.position);
            boidsCompute.SetFloat(TargetWeight, targetWeight);
            boidsCompute.SetFloat(FloorY, floorY);
            boidsCompute.SetFloat(AvoidanceMargin, floorAvoidanceMargin);

            int count = activePredators.Count;
            for (int i = 0; i < count; i++)
            {
                predatorDataArray[i].position = activePredators[i].transform.position;
                predatorDataArray[i].radius = activePredators[i].panicRadius;
            }

            predatorsBuffer.SetData(predatorDataArray, 0, 0, count);
            boidsCompute.SetInt(NumPredators, count);

            int totalCells = gridSize * gridSize * gridSize;
            int gridThreadGroups = (totalCells + 63) / 64;
            int boidThreadGroups = (numBoids + 63) / 64;
            int paddedThreadGroups = (paddedCount + 63) / 64;

            // --- 2. THE SORTING PASS (Runs every N frames) ---
            if (frameCount % sortFrequency == 0)
            {
                boidsCompute.Dispatch(clearGridKernel, gridThreadGroups, 1, 1);

                // Populate hashes from our current state
                boidsCompute.SetBuffer(populateHashesKernel, ReadBoidsBuffer, currentBuffer);
                boidsCompute.Dispatch(populateHashesKernel, paddedThreadGroups, 1, 1);

                GPUSort.Sort(bitonicSortCompute, sortBuffer, paddedCount);

                // Physically reorder 'currentBuffer' into 'nextBuffer'
                boidsCompute.SetBuffer(reorderBoidsKernel, ReadBoidsBuffer, currentBuffer);
                boidsCompute.SetBuffer(reorderBoidsKernel, WriteBoidsBuffer, nextBuffer);
                boidsCompute.Dispatch(reorderBoidsKernel, boidThreadGroups, 1, 1);

                boidsCompute.Dispatch(buildOffsetsKernel, boidThreadGroups, 1, 1);

                // CRITICAL FIX: 'nextBuffer' is now perfectly sorted! 'currentBuffer' is a mess.
                // We swap them manually so CSMain reads the cleanly sorted data.
                (currentBuffer, nextBuffer) = (nextBuffer, currentBuffer);
            }

            // --- 3. THE PHYSICS PASS (Runs EVERY frame) ---
            // Read the current state, calculate physics, write into the empty scratchpad (nextBuffer)
            boidsCompute.SetBuffer(csMainKernel, ReadBoidsBuffer, currentBuffer);
            boidsCompute.SetBuffer(csMainKernel, WriteBoidsBuffer, nextBuffer);
            boidsCompute.Dispatch(csMainKernel, boidThreadGroups, 1, 1);

            // --- 4. PREPARE FOR RENDER ---
            // nextBuffer now contains the newly calculated frame. 
            // Swap references so nextBuffer becomes currentBuffer for rendering and the next frame!
            (currentBuffer, nextBuffer) = (nextBuffer, currentBuffer);

            // Render the final calculated state
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
        }

        public void RegisterPredator(BoidPredator predator)
        {
            if (!activePredators.Contains(predator) && activePredators.Count < maxPredators)
                activePredators.Add(predator);
        }

        public void UnregisterPredator(BoidPredator predator)
        {
            activePredators.Remove(predator);
        }

        private void UpdateObstacles()
        {
            int size = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, collBuffer, obstacleLayer);
            obstacleCount = Mathf.Min(size, maxObstacles);
            for (int i = 0; i < obstacleCount; i++)
            {
                obstaclesArray[i].position = collBuffer[i].bounds.center;
                obstaclesArray[i].radius = collBuffer[i].bounds.extents.magnitude;
            }

            obstaclesBuffer.SetData(obstaclesArray);
            boidsCompute.SetInt(NumObstacles, obstacleCount);
        }

        private struct PredatorData
        {
            public Vector3 position;
            public float radius;
        }

        private struct Boid
        {
            public Vector3 position;
            public Vector3 direction;
            public Vector3 color;
            public float size;
            public float currentSpeed;
            public float roll;
            public float flapPhase;
        }

        private struct Obstacle
        {
            public Vector3 position;
            public float radius;
        }
    }
}