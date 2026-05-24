using UnityEngine;

namespace TechArtPlayground
{
    public class BoidsManager : MonoBehaviour
    {
        // Property IDs for slight performance gain over strings
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
        private static readonly int PredatorPosition = Shader.PropertyToID("predatorPosition");
        private static readonly int PredatorRadius = Shader.PropertyToID("predatorRadius");

        struct Boid { public Vector3 position; public Vector3 direction; public Vector3 color; public float size; public float currentSpeed; public float roll; public float flapPhase;
        }
        struct Obstacle { public Vector3 position; public float radius; }

        [Header("References")]
        public ComputeShader boidsCompute;
        public ComputeShader bitonicSortCompute; // NOUVEAU: Add this in inspector!
        public Mesh fishMesh;
        public Material fishMaterial;
        public Transform playerTransform;

        [Header("Banc Parameters")]
        public int numBoids = 10000;
        private int paddedCount; // Power of 2 requirement
        public float spawnRadius = 40f;
        public float speed = 5f;
        public float sightRadius = 3f;
        public float separationWeight = 1.5f;
        public float targetWeight = 0.5f;

        [Header("Spatial Grid")]
        public float cellSize = 3f;
        public int gridSize = 64; 

        [Header("Variations Organiques")]
        public float separationPulseSpeed = 1.0f; 
        public float separationPulseAmount = 0.8f; 

        [Header("Visuals")]
        public Color colorA = Color.white;
        public Color colorB = Color.blue;
        public float minSize = 0.5f;
        public float maxSize = 1.5f;
        public float predatorRadius = 5f;

        [Header("World Limits")]
        public float floorY = 0f; 
        public float floorAvoidanceMargin = 2f; 
        public LayerMask obstacleLayer;
        public int maxObstacles = 50;
        public float scanRadius = 50f;

        private ComputeBuffer boidsBuffer;
        private ComputeBuffer argsBuffer;
        private ComputeBuffer obstaclesBuffer; 
        private ComputeBuffer gridOffsetsBuffer;
        private ComputeBuffer sortBuffer; // NOUVEAU

        private Obstacle[] obstaclesArray;
        private Collider[] collBuffer = new Collider[32];
    
        private int clearGridKernel, populateHashesKernel, buildOffsetsKernel, csMainKernel;
        private int obstacleCount = 0;
        
        // Physics Throttle
        private float obstacleScanTimer = 0f;
        private const float OBSTACLE_SCAN_RATE = 10f;

        void Start()
        {
            clearGridKernel = boidsCompute.FindKernel("ClearGrid");
            populateHashesKernel = boidsCompute.FindKernel("PopulateHashes");
            buildOffsetsKernel = boidsCompute.FindKernel("BuildGridOffsets");
            csMainKernel = boidsCompute.FindKernel("CSMain");

            // Calculate Power of 2 for Sorting
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

            boidsBuffer = new ComputeBuffer(numBoids, 52);
            boidsBuffer.SetData(boidsArray);

            // 2. Spatial Grid Buffers
            int totalCells = gridSize * gridSize * gridSize;
            gridOffsetsBuffer = new ComputeBuffer(totalCells, sizeof(int));
            sortBuffer = new ComputeBuffer(paddedCount, sizeof(uint) * 2);

            // 3. Obstacle Buffers
            obstaclesArray = new Obstacle[maxObstacles];
            obstaclesBuffer = new ComputeBuffer(maxObstacles, 16); 

            // 4. Assign Buffers
            boidsCompute.SetBuffer(clearGridKernel, "gridOffsets", gridOffsetsBuffer);
        
            boidsCompute.SetBuffer(populateHashesKernel, "boidsBuffer", boidsBuffer);
            boidsCompute.SetBuffer(populateHashesKernel, "SortBuffer", sortBuffer);

            boidsCompute.SetBuffer(buildOffsetsKernel, "SortBuffer", sortBuffer);
            boidsCompute.SetBuffer(buildOffsetsKernel, "gridOffsets", gridOffsetsBuffer);

            boidsCompute.SetBuffer(csMainKernel, "boidsBuffer", boidsBuffer);
            boidsCompute.SetBuffer(csMainKernel, "gridOffsets", gridOffsetsBuffer);
            boidsCompute.SetBuffer(csMainKernel, "SortBuffer", sortBuffer);
            boidsCompute.SetBuffer(csMainKernel, "obstaclesBuffer", obstaclesBuffer);

            fishMaterial.SetBuffer("boidsBuffer", boidsBuffer);

            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            args[0] = (uint)fishMesh.GetIndexCount(0);
            args[1] = (uint)numBoids;
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(args);
            
            // Initial scan
            UpdateObstacles();
        }

        void Update()
        {
            // CPU OPTIMIZATION: Throttle physics scan
            obstacleScanTimer += Time.deltaTime;
            if (obstacleScanTimer >= OBSTACLE_SCAN_RATE)
            {
                obstacleScanTimer = 0f;
                UpdateObstacles();
            }

            float pulse = Mathf.Sin(Time.time * separationPulseSpeed) * separationPulseAmount;
            float dynamicSeparation = Mathf.Max(0.1f, separationWeight + pulse);

            // Send globals
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

            if (playerTransform is not null)
            {
                boidsCompute.SetVector(PredatorPosition, playerTransform.position);
                boidsCompute.SetFloat(PredatorRadius, predatorRadius);
            }

            // CPU OPTIMIZATION: Integer division instead of floats and CeilToInt
            int totalCells = gridSize * gridSize * gridSize;
            int gridThreadGroups = (totalCells + 63) / 64;
            int boidThreadGroups = (numBoids + 63) / 64;
            int paddedThreadGroups = (paddedCount + 63) / 64;

            // --- THE 5-STEP GPU PIPELINE ---
            
            // Step 1: Clear grid
            boidsCompute.Dispatch(clearGridKernel, gridThreadGroups, 1, 1);
        
            // Step 2: Hash calculation (Uses paddedCount)
            boidsCompute.Dispatch(populateHashesKernel, paddedThreadGroups, 1, 1);
        
            // Step 3: Execute Bitonic Sort
            GPUSort.Sort(bitonicSortCompute, sortBuffer, paddedCount);

            // Step 4: Map the sorted array to grid offsets (Uses numBoids)
            boidsCompute.Dispatch(buildOffsetsKernel, boidThreadGroups, 1, 1);

            // Step 5: Execute boid physics
            boidsCompute.Dispatch(csMainKernel, boidThreadGroups, 1, 1);

            // ---------------------------------------------------

            Graphics.DrawMeshInstancedIndirect(fishMesh, 0, fishMaterial, new Bounds(transform.position, Vector3.one * 1000f), argsBuffer);
        }
        
        private void UpdateObstacles()
        {
            var size = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, collBuffer, obstacleLayer);
            obstacleCount = Mathf.Min(size, maxObstacles);

            for (int i = 0; i < obstacleCount; i++)
            {
                obstaclesArray[i].position = collBuffer[i].bounds.center;
                obstaclesArray[i].radius = collBuffer[i].bounds.extents.magnitude; 
            }

            obstaclesBuffer.SetData(obstaclesArray);
            boidsCompute.SetInt(NumObstacles, obstacleCount);
        }

        void OnDestroy()
        {
            if (boidsBuffer != null) boidsBuffer.Release();
            if (argsBuffer != null) argsBuffer.Release();
            if (obstaclesBuffer != null) obstaclesBuffer.Release();
            if (gridOffsetsBuffer != null) gridOffsetsBuffer.Release();
            if (sortBuffer != null) sortBuffer.Release();
        }
    }
}