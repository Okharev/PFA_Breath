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
    
    public class BoidSwarm : MonoBehaviour
    {
        [Header("Rendering")] public Mesh swarmMesh;

        public Material swarmMaterial;

        [Header("Swarm Configuration")] public int boidCount = 5000;

        public Bounds swarmBounds = new(Vector3.zero, new Vector3(100, 100, 100));
        public bool followSpline;

        [Header("Dynamic Environment Limits")] public int maxAttractors = 16;

        public int maxPredators = 16;


        [Header("Spline Settings")] 
        public SplineContainer splineContainer;
        [Tooltip("How many points to bake. Higher = smoother curves but more VRAM.")]
        public int splineResolution = 100;
        [Range(0.1f, 10f)] public float tubeRadius = 2.0f;

        public float splineLength { get; private set; }
        
        [Header("Flocking Behaviors")] [Range(0.1f, 20f)]
        public float speed = 4.0f;
        
        

        [Header("Optimization")]
        [Tooltip(
            "How often to update the spatial grid. Higher = better FPS, but boids might clip if moving very fast.")]
        [Range(1, 10)]
        public int sortFrequency = 4;

        [Range(0.1f, 10f)] public float sightRadius = 2.5f;
        [Range(0.0f, 5f)] public float separationWeight = 1.5f;
        [Range(0.0f, 5f)] public float alignmentWeight = 1.0f;
        [Range(0.0f, 5f)] public float cohesionWeight = 1.5f;

        [Header("Environment & Avoidance")] public float floorY = -10f;

        public float avoidanceMargin = 2.0f;
        [Range(0f, 10f)] public float predatorFleeWeight = 5.0f;

        [Header("Attractors & Waypoints")] public Vector3 defaultWaypoint = Vector3.zero;

        [Range(0f, 5f)] public float targetWeight = 1.0f;
        public float swirlStrength = 2.0f;
        public float arrivalRadiusSq = 25.0f; // Distance squared (e.g., 5 units)
        public float arrivalMinSpeed = 0.5f;
        public float singularitySoften = 1.0f;


        [Header("Spatial Grid Tuning")] [Tooltip("Must be equal to or slightly larger than the Sight Radius!")]
        public float cellSize = 3.0f;

        [Tooltip("How many cells per axis. Higher = larger world area, but uses more VRAM.")]
        public int gridSize = 64;

// Internal Tracking Lists
        private readonly List<BoidAttractor> activeAttractors = new();
        private readonly List<BoidPredator> activePredators = new();

// Data Caches to prevent memory allocations in the Update loop
        private AttractorData[] attractorDataCache;
        private PredatorData[] predatorDataCache;
        private Vector3[] previousAttractorPositions;
        public int frameOffset { get; private set; }

        public int CurrentAttractorCount => Mathf.Min(activeAttractors.Count, maxAttractors);
        public int CurrentPredatorCount => Mathf.Min(activePredators.Count, maxPredators);

// 2. Add the buffer variable
        public GraphicsBuffer obstaclesBuffer { get; private set; }


        // Buffers
        public GraphicsBuffer readBuffer { get; private set; }
        public GraphicsBuffer writeBuffer { get; private set; }
        public GraphicsBuffer sortBuffer { get; private set; }

        public GraphicsBuffer gridOffsets { get; private set; }
        public GraphicsBuffer splineBuffer { get; private set; }
        public GraphicsBuffer argsBuffer { get; private set; }
        
        public GraphicsBuffer tempSortBuffer { get; private set; }
        public GraphicsBuffer globalHistBuffer { get; private set; }
        public GraphicsBuffer localOffsetsBuffer { get; private set; }

        // New Environment Buffers
        public GraphicsBuffer attractorsBuffer { get; private set; }
        public GraphicsBuffer predatorsBuffer { get; private set; }

        public int paddedCount { get; private set; }

        private void OnDestroy()
        {
            readBuffer?.Release();
            writeBuffer?.Release();
            sortBuffer?.Release();
            gridOffsets?.Release();
            splineBuffer?.Release();
            argsBuffer?.Release();
            attractorsBuffer?.Release();
            predatorsBuffer?.Release();
            obstaclesBuffer?.Release();
            tempSortBuffer?.Release();
            globalHistBuffer?.Release();
            localOffsetsBuffer?.Release();
            tempSortBuffer?.Release();
            globalHistBuffer?.Release();
            localOffsetsBuffer?.Release();
        }

// --- REGISTRATION METHODS ---
        public void RegisterAttractor(BoidAttractor a)
        {
            if (!activeAttractors.Contains(a)) activeAttractors.Add(a);
        }

        public void UnregisterAttractor(BoidAttractor a)
        {
            activeAttractors.Remove(a);
        }

        public void RegisterPredator(BoidPredator p)
        {
            if (!activePredators.Contains(p)) activePredators.Add(p);
        }

        public void UnregisterPredator(BoidPredator p)
        {
            activePredators.Remove(p);
        }

        public void Initialize()
        {
            defaultWaypoint = transform.position;

            // Assign a random frame offset to prevent multiple swarms from sorting on the exact same frame!
            frameOffset = Random.Range(0, 10);

            paddedCount = Mathf.NextPowerOfTwo(boidCount);

            // Initialize Caches
            attractorDataCache = new AttractorData[maxAttractors];
            predatorDataCache = new PredatorData[maxPredators];
            previousAttractorPositions = new Vector3[maxAttractors];

            // 1. Allocate fixed-size GPU buffers for dynamic components
            int safeAttCount = Mathf.Max(1, maxAttractors);
            int safePredCount = Mathf.Max(1, maxPredators);

            attractorsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, safeAttCount, 32);
            predatorsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, safePredCount, 16);

// 2. Core Data Buffers
            readBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidCount, 48);
            writeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidCount, 48);
            sortBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, paddedCount, 8);

// NEW RADIX BUFFERS
            int numBlocks = Mathf.Max(1, Mathf.CeilToInt(paddedCount / 256f));
            tempSortBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, paddedCount, 8); 
            globalHistBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 16 * numBlocks, 4); // 16 bins * blocks
            localOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, paddedCount, 4);


// 256 bins * numBlocks (uints)
            globalHistBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 256 * numBlocks, 4); 

// Cache for localized scatter offsets (uints)
            localOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, paddedCount, 4);
// NEW
            int totalGridCells = gridSize * gridSize * gridSize;
            gridOffsets = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalGridCells, 4);

            obstaclesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 16);
            obstaclesBuffer.SetData(new[] { new ObstacleData() });

            // 3. Indirect Arguments Buffer
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
            else
            {
                Debug.LogWarning($"[BoidSwarm] No Mesh assigned on {gameObject.name}! Fish will not render.");
            }

            argsBuffer.SetData(args);
            
            if (followSpline && splineContainer != null)
            {
                splineLength = splineContainer.CalculateLength();
                SplineSampleData[] bakedSpline = new SplineSampleData[splineResolution];

                for (int i = 0; i < splineResolution; i++)
                {
                    // Calculate normalized t (0.0 to 1.0)
                    float t = i / (float)(splineResolution - 1);
            
                    bakedSpline[i] = new SplineSampleData
                    {
                        position = splineContainer.EvaluatePosition(t),
                        tangent = ((Vector3)splineContainer.EvaluateTangent(t)).normalized,
                        width = tubeRadius
                    };
                }

                // Allocate 28-byte stride (Vector3 + Vector3 + float)
                splineBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, splineResolution, 28);
                splineBuffer.SetData(bakedSpline);
            }
            else if (followSpline && splineContainer == null)
            {
                Debug.LogWarning($"[BoidSwarm] {gameObject.name} is set to Follow Spline, but no SplineContainer is assigned!");
                followSpline = false; // Fallback to free-roam to prevent GPU crash
            }

            PopulateInitialData();
        }

        public void SyncEnvironmentData()
        {
            // 1. Sync Attractors
            int aCount = CurrentAttractorCount;
            if (aCount > 0)
            {
                for (int i = 0; i < aCount; i++)
                {
                    BoidAttractor attr = activeAttractors[i];
                    Vector3 currentPos = attr.transform.position;

                    // Calculate velocity manually since it might not have a Rigidbody
                    Vector3 velocity = (currentPos - previousAttractorPositions[i]) / Time.deltaTime;
                    previousAttractorPositions[i] = currentPos;

                    attractorDataCache[i] = new AttractorData
                    {
                        position = currentPos,
                        weight = attr.weight,
                        velocity = velocity,
                        padding = 0f
                    };
                }

                attractorsBuffer.SetData(attractorDataCache, 0, 0, aCount);
            }

            // 2. Sync Predators
            int pCount = CurrentPredatorCount;
            if (pCount > 0)
            {
                for (int i = 0; i < pCount; i++)
                {
                    BoidPredator pred = activePredators[i];
                    predatorDataCache[i] = new PredatorData
                    {
                        position = pred.transform.position,
                        radiusSq = pred.panicRadius * pred.panicRadius
                    };
                }

                predatorsBuffer.SetData(predatorDataCache, 0, 0, pCount);
            }
        }

        private void PopulateInitialData()
        {
            Boid[] boids = new Boid[boidCount];
            for (int i = 0; i < boidCount; i++)
            {
                // Map an initial roll of 0.0 to the middle of our 16-bit range (0 to 65535)
                ushort roll16 = (ushort)(0.5f * 65535f); 
                ushort id16 = (ushort)i;
        
                // Bitwise shift the ID into the top 16 bits, leave roll in the bottom 16 bits
                uint packed = ((uint)id16 << 16) | roll16;

                boids[i] = new Boid
                {
                    position = transform.position + Random.insideUnitSphere * 10f,
                    randomSeed = Random.value, // Precompute Size/Wag Seed (0.0 to 1.0)
    
                    velocity = Random.onUnitSphere,
                    colorSeed = Random.value,  // Precompute Color Seed (0.0 to 1.0)
    
                    packedData = packed, 
                    splineT = Random.value,
                    pad1 = 0f, 
                    pad2 = 0f
                };
            }
            readBuffer.SetData(boids);
            writeBuffer.SetData(boids);
        }

        public void PingPongBuffers()
        {
            (writeBuffer, readBuffer) = (readBuffer, writeBuffer);
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct ObstacleData
        {
            public Vector3 position;
            public float radiusSq;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Boid
        {
            public Vector3 position;    // 12 bytes
            public float randomSeed;    // 4 bytes  (Total 16)
    
            public Vector3 velocity;    // 12 bytes
            public float colorSeed;     // 4 bytes  (Total 32)
    
            public uint packedData;     // 4 bytes  (ID + Roll)
            public float splineT;       // 4 bytes
            public float pad1;          // 4 bytes  (Alignment padding)
            public float pad2;          // 4 bytes  (Total 48 bytes)
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
    }
}