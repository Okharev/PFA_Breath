using System.Collections.Generic;
using System.Runtime.InteropServices;
using TechArtPlayground.Cloth;
using UnityEngine;
using UnityEngine.Rendering;

namespace TechArtPlayground.Wind.Cloth
{
    public class PhysicsBannerManager : MonoBehaviour
    {
        [Header("Resources")]
        public ComputeShader clothCompute;
        public Material clothMaterial;

        [Header("XPBD Physics Settings")]
        public Vector3 gravity = new(0, -9.81f, 0);

        [Tooltip("How much air resistance the cloth has. 2 = normal, 5 = heavy/damped, 10 = moving underwater.")]
        [Range(0f, 15f)]
        public float drag = 2.5f;
        
        [Tooltip("0 = perfectly stiff cloth. Use extremely small values (e.g., 0.000001) if you want elastic stretch.")]
        public float springCompliance = 0.0f; 

        [Tooltip("Higher iterations = stiffer cloth. Minimum 20 recommended for SOR Jacobi.")]
        [Range(1, 40)] 
        public int solverIterations = 20;

        // --- Kernels ---
        private int _kernelPredict, _kernelSolveConstraints, _kernelIntegrate, _kernelComputeNormals;

        // --- Live Collider Tracking ---
        private ClothStaticCollider[] _activeColliders;
        private ClothColliderData[] _colliderDataArray;

        // --- Core SoA Buffers ---
        private GraphicsBuffer _positionsBuffer;
        private GraphicsBuffer _predictedPositionsBuffer;
        private GraphicsBuffer _normalsBuffer;
        private GraphicsBuffer _uvsBuffer;
        private GraphicsBuffer _physicsBuffer;
        private GraphicsBuffer _springLinksBuffer;
        private GraphicsBuffer _springsBuffer;
        private GraphicsBuffer _adjacencyBuffer;
        private GraphicsBuffer _collidersBuffer;

        private Mesh _megaMesh;
        private int _totalVertices, _totalTriangles;

        // --- Timestep Accumulator ---
        private float _timeAccumulator = 0.0f;
        private const float FixedTimeStep = 0.01666f; // Target 60hz physics clock (1 / 60)

        private void Start()
        {
            InitializeMegaSimulation();
        }

        private void Update()
        {
            if (_positionsBuffer == null) return;

            // 1. UPDATE DYNAMIC COLLIDERS (Push transform changes to GPU)
            UpdateDynamicColliders();

            // 2. TIMESTEP ACCUMULATOR (Prevents explosions during frame drops)
            _timeAccumulator += Mathf.Min(Time.deltaTime, 0.1f); // Cap max spike to prevent spiral of death

            while (_timeAccumulator >= FixedTimeStep)
            {
                StepSimulation(FixedTimeStep);
                _timeAccumulator -= FixedTimeStep;
            }

            // 3. DRAW MESH (Shader reads directly from SoA buffers)
            Graphics.DrawMesh(_megaMesh, Matrix4x4.identity, clothMaterial, gameObject.layer);
        }

        private void OnDestroy()
        {
            // Release memory to prevent memory leaks
            _positionsBuffer?.Dispose();
            _predictedPositionsBuffer?.Dispose();
            _normalsBuffer?.Dispose();
            _uvsBuffer?.Dispose();
            _physicsBuffer?.Dispose();
            _springLinksBuffer?.Dispose();
            _springsBuffer?.Dispose();
            _adjacencyBuffer?.Dispose();
            _collidersBuffer?.Dispose();
        }

        private void StepSimulation(float dt)
        {
            float subStepDelta = dt / solverIterations;
            
            clothCompute.SetFloat("_DeltaTime", subStepDelta);
            clothCompute.SetFloat("_Time", Time.time);
            clothCompute.SetVector("_Gravity", gravity);
            clothCompute.SetFloat("_Drag", drag); // <--- ADD THIS LINE
            
            // Link directly to WeatherManager Singleton
            if (WeatherManager.Instance != null)
            {
                clothCompute.SetVector("_WindVelocity", WeatherManager.Instance.CurrentWindVelocity);
                clothCompute.SetFloat("_WindTurbulence", WeatherManager.Instance.windGusts);
            }
            else
            {
                clothCompute.SetVector("_WindVelocity", Vector3.zero);
                clothCompute.SetFloat("_WindTurbulence", 0f);
            }

            int groupsX = Mathf.CeilToInt(_totalVertices / 64f);

            for (int i = 0; i < solverIterations; i++)
            {
                // A. Predict Euler (Forces & Wind)
                clothCompute.Dispatch(_kernelPredict, groupsX, 1, 1);
                
                // B. Solve Distance Constraints & Static Collisions (Jacobi XPBD)
                clothCompute.Dispatch(_kernelSolveConstraints, groupsX, 1, 1);
                
                // C. Integrate Velocities
                clothCompute.Dispatch(_kernelIntegrate, groupsX, 1, 1);
            }

            // Lock-Free Normal calculation (Done once per frame, not per sub-step)
            clothCompute.Dispatch(_kernelComputeNormals, groupsX, 1, 1);
        }

        private void UpdateDynamicColliders()
        {
            if (_activeColliders == null || _activeColliders.Length == 0) return;

            for (int i = 0; i < _activeColliders.Length; i++)
            {
                ClothStaticCollider c = _activeColliders[i];

                _colliderDataArray[i].type = (int)c.colliderType;
                _colliderDataArray[i].radius = c.radius;

                if (c.colliderType == ClothColliderType.Sphere)
                {
                    _colliderDataArray[i].posA = c.transform.position;
                }
                else if (c.colliderType == ClothColliderType.Capsule)
                {
                    Vector3 up = c.transform.up;
                    float halfHeight = Mathf.Max(0, c.height * 0.5f - c.radius);
                    _colliderDataArray[i].posA = c.transform.position + up * halfHeight;
                    _colliderDataArray[i].posB = c.transform.position - up * halfHeight;
                }
                else if (c.colliderType == ClothColliderType.Box)
                {
                    _colliderDataArray[i].worldToLocal = c.transform.worldToLocalMatrix;
                    _colliderDataArray[i].localToWorld = c.transform.localToWorldMatrix;
                    _colliderDataArray[i].extents = c.boxExtents;
                }
            }

            // Immediately upload updated transform matrices/positions to the GPU
            _collidersBuffer.SetData(_colliderDataArray);
        }

        private void InitializeMegaSimulation()
{
    // Find all banners in the scene
    PhysicsBannerNode[] nodes = FindObjectsByType<PhysicsBannerNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    if (nodes.Length == 0) return;

    List<Vector3> positions = new();
    List<Vector3> normals = new();
    List<Vector2> uvs = new();
    List<PhysicsState> physics = new();
    List<SpringLink> springLinks = new();
    List<Spring> springs = new();
    List<Int4> adjacency = new();
    List<int> meshIndices = new();

    List<ClothStaticCollider> safeColliderTracker = new();
    List<ClothColliderData> globalCollidersData = new();

    int vertexOffset = 0;

    foreach (PhysicsBannerNode node in nodes)
    {
        // ==========================================
        // --- 1. PROCESS DYNAMIC COLLIDERS ---
        // ==========================================
        ClothStaticCollider[] nodeColliders = node.GetComponentsInChildren<ClothStaticCollider>();
        uint colliderStart = (uint)safeColliderTracker.Count;
        uint colliderCount = (uint)nodeColliders.Length;

        foreach (ClothStaticCollider c in nodeColliders)
        {
            safeColliderTracker.Add(c);

            ClothColliderData cData = new()
            {
                type = (int)c.colliderType,
                radius = c.radius
            };

            if (c.colliderType == ClothColliderType.Sphere) cData.posA = c.transform.position;
            else if (c.colliderType == ClothColliderType.Capsule)
            {
                Vector3 up = c.transform.up;
                float halfHeight = Mathf.Max(0, c.height * 0.5f - c.radius);
                cData.posA = c.transform.position + up * halfHeight;
                cData.posB = c.transform.position - up * halfHeight;
            }
            else if (c.colliderType == ClothColliderType.Box)
            {
                cData.worldToLocal = c.transform.worldToLocalMatrix;
                cData.localToWorld = c.transform.localToWorldMatrix;
                cData.extents = c.boxExtents;
            }

            globalCollidersData.Add(cData);
        }

        int nodeVertexCount = node.resolution.x * node.resolution.y;
        Vector2 step = new(node.dimensions.x / (node.resolution.x - 1), node.dimensions.y / (node.resolution.y - 1));

        // ==========================================
        // --- 2. VERTICES, PHYSICS & WEIGHT MAP ---
        // ==========================================
        for (int y = 0; y < node.resolution.y; y++)
        {
            for (int x = 0; x < node.resolution.x; x++)
            {
                int index = y * node.resolution.x + x;
                Vector3 localPos = new(x * step.x - node.dimensions.x * 0.5f, -(y * step.y), 0);
                Vector3 worldPos = node.transform.TransformPoint(localPos);

                float uCoord = (float)x / (node.resolution.x - 1);
                float vCoord = 1.0f - (float)y / (node.resolution.y - 1);

                positions.Add(worldPos);
                normals.Add(-node.transform.forward);
                uvs.Add(new Vector2(uCoord, vCoord));

                // Base parameters
                float invMass = (y == 0) ? 0.0f : 1.0f;
                float stiffnessMultiplier = 1.0f;
                float selfCollide = 1.0f; // Default: Self-collision enabled

                if (node.weightMap != null)
                {
                    int texX = Mathf.Clamp(Mathf.RoundToInt(uCoord * (node.weightMap.width - 1)), 0, node.weightMap.width - 1);
                    int texY = Mathf.Clamp(Mathf.RoundToInt(vCoord * (node.weightMap.height - 1)), 0, node.weightMap.height - 1);
    
                    Color paintColor = node.weightMap.GetPixel(texX, texY);
    
                    invMass = paintColor.r; // Red controls mass/pinning
                    stiffnessMultiplier = Mathf.Lerp(1.0f, 0.1f, paintColor.g); // Green controls stiffness
    
                    // NEW: Blue controls the self-collision mask!
                    selfCollide = paintColor.b; 
                }

                physics.Add(new PhysicsState 
                { 
                    velocity = Vector3.zero, 
                    inverseMass = invMass,
                    colliderStart = colliderStart,
                    colliderCount = colliderCount,
                    selfCollideMask = selfCollide, // Pushed to the GPU here!
                    padding = 0f
                });

                // Lock-Free Adjacency (x=left, y=right, z=top, w=bottom)
                Int4 adj = new Int4(-1, -1, -1, -1);
                if (x > 0) adj.x = vertexOffset + index - 1;
                if (x < node.resolution.x - 1) adj.y = vertexOffset + index + 1;
                if (y > 0) adj.z = vertexOffset + index - node.resolution.x;
                if (y < node.resolution.y - 1) adj.w = vertexOffset + index + node.resolution.x;
                
                // Safety cut for Prayer Flags in Adjacency to prevent weird normals at the seam
                if (node.isPrayerFlagMode)
                {
                    if (x > 0 && (x / node.flagWidth) != ((x - 1) / node.flagWidth)) adj.x = -1;
                    if (x < node.resolution.x - 1 && (x / node.flagWidth) != ((x + 1) / node.flagWidth)) adj.y = -1;
                }
                
                adjacency.Add(adj);
            }
        }

        // ==========================================
        // --- 3. SPRINGS (With Safe Cuts & Stiffness) ---
        // ==========================================
        for (int y = 0; y < node.resolution.y; y++)
        {
            for (int x = 0; x < node.resolution.x; x++)
            {
                int index = y * node.resolution.x + x;
                uint startIndex = (uint)springs.Count;
                uint springCount = 0;

                // Grab Green Channel stiffness if available
                float stiffnessMultiplier = 1.0f;
                if (node.weightMap != null)
                {
                    float uCoord = (float)x / (node.resolution.x - 1);
                    float vCoord = 1.0f - (float)y / (node.resolution.y - 1);
                    int texX = Mathf.Clamp(Mathf.RoundToInt(uCoord * (node.weightMap.width - 1)), 0, node.weightMap.width - 1);
                    int texY = Mathf.Clamp(Mathf.RoundToInt(vCoord * (node.weightMap.height - 1)), 0, node.weightMap.height - 1);
                    
                    Color paintColor = node.weightMap.GetPixel(texX, texY);
                    
                    // Green Channel = Stiffness Gradient. 1.0 makes it 10x stiffer. 0.0 is normal.
                    stiffnessMultiplier = Mathf.Lerp(1.0f, 0.1f, paintColor.g); 
                }

                void AddSpring(int nx, int ny, float compMult)
                {
                    if (nx >= 0 && nx < node.resolution.x && ny >= 0 && ny < node.resolution.y)
                    {
                        bool bothRope = node.isPrayerFlagMode && y == 0 && ny == 0;
                        
                        // SAFER CUT LOGIC: Prevent ANY spring from crossing the flag gap
                        if (node.isPrayerFlagMode && !bothRope)
                        {
                            int currentFlagIdx = x / node.flagWidth;
                            int neighborFlagIdx = nx / node.flagWidth;
                            if (currentFlagIdx != neighborFlagIdx) return; // Cut the spring!
                        }

                        int neighborIdx = ny * node.resolution.x + nx;
                        float dist = Vector3.Distance(positions[vertexOffset + index], positions[vertexOffset + neighborIdx]);

                        springs.Add(new Spring
                        {
                            targetIndex = (uint)(neighborIdx + vertexOffset),
                            restLength = dist,
                            compliance = springCompliance * compMult * stiffnessMultiplier,
                            padding = 0
                        });
                        springCount++;
                    }
                }

                // 1. Structural (Distance 1)
                AddSpring(x, y - 1, 1.0f); AddSpring(x, y + 1, 1.0f);
                AddSpring(x - 1, y, 1.0f); AddSpring(x + 1, y, 1.0f);
                
                // 2. Shear (Diagonals)
                AddSpring(x - 1, y - 1, 2.0f); AddSpring(x + 1, y - 1, 2.0f);
                AddSpring(x - 1, y + 1, 2.0f); AddSpring(x + 1, y + 1, 2.0f);
                
                // 3. Bending (Distance 2 - Prevents Ribbons from Tangling)
                AddSpring(x, y - 2, 4.0f); AddSpring(x, y + 2, 4.0f);
                AddSpring(x - 2, y, 4.0f); AddSpring(x + 2, y, 4.0f);

                springLinks.Add(new SpringLink { startIndex = startIndex, count = springCount });
            }
        }

        // ==========================================
        // --- 4. TRIANGLES ---
        // ==========================================
        for (int y = 0; y < node.resolution.y - 1; y++)
        {
            for (int x = 0; x < node.resolution.x - 1; x++)
            {
                // Do not draw geometry across the prayer flag cuts
                if (node.isPrayerFlagMode && (x + 1) % node.flagWidth == 0) continue;

                int i0 = vertexOffset + y * node.resolution.x + x;
                int i1 = i0 + 1;
                int i2 = i0 + node.resolution.x;
                int i3 = i2 + 1;

                meshIndices.Add(i0); meshIndices.Add(i2); meshIndices.Add(i1);
                meshIndices.Add(i1); meshIndices.Add(i2); meshIndices.Add(i3);
            }
        }

        vertexOffset += nodeVertexCount;
    }

    // Set up Arrays
    _activeColliders = safeColliderTracker.ToArray();
    _colliderDataArray = globalCollidersData.ToArray();

    _totalVertices = positions.Count;
    _totalTriangles = meshIndices.Count / 3;

    // Generate Dummy Mesh topology for Graphics.DrawMesh
    _megaMesh = new Mesh { name = "MegaClothMesh", indexFormat = IndexFormat.UInt32 };
    _megaMesh.SetVertices(new Vector3[_totalVertices]); 
    _megaMesh.SetIndices(meshIndices.ToArray(), MeshTopology.Triangles, 0);
    _megaMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f); 

    // ==========================================
    // --- 5. ALLOCATE GPU SoA BUFFERS ---
    // ==========================================
    _positionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 12);
    _predictedPositionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 12);
    _normalsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 12);
    _uvsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 8);
    _physicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 32); 
    _springLinksBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 8);
    _springsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, springs.Count, 16);
    _adjacencyBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 16);

    // Colliders Buffer Validation
    if (_activeColliders.Length > 0)
    {
        _collidersBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _colliderDataArray.Length, 192);
        _collidersBuffer.SetData(_colliderDataArray);
    }
    else
    {
        _collidersBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 192);
        _collidersBuffer.SetData(new[] { new ClothColliderData() });
    }

    // Push Data to GPU
    _positionsBuffer.SetData(positions);
    _predictedPositionsBuffer.SetData(positions);
    _normalsBuffer.SetData(normals);
    _uvsBuffer.SetData(uvs);
    _physicsBuffer.SetData(physics);
    _springLinksBuffer.SetData(springLinks);
    _springsBuffer.SetData(springs);
    _adjacencyBuffer.SetData(adjacency);

    // ==========================================
    // --- 6. SETUP & BIND COMPUTE SHADER ---
    // ==========================================
    _kernelPredict = clothCompute.FindKernel("CSPredict");
    _kernelSolveConstraints = clothCompute.FindKernel("CSSolveConstraints");
    _kernelIntegrate = clothCompute.FindKernel("CSIntegrate");
    _kernelComputeNormals = clothCompute.FindKernel("CSComputeNormals");

    clothCompute.SetInt("_VertexCount", _totalVertices);

    // Bind Predict
    clothCompute.SetBuffer(_kernelPredict, "Positions", _positionsBuffer);
    clothCompute.SetBuffer(_kernelPredict, "PredictedPositions", _predictedPositionsBuffer);
    clothCompute.SetBuffer(_kernelPredict, "PhysicsData", _physicsBuffer);
    clothCompute.SetBuffer(_kernelPredict, "Normals", _normalsBuffer);

    // Bind Solve Constraints
    clothCompute.SetBuffer(_kernelSolveConstraints, "PredictedPositions", _predictedPositionsBuffer);
    clothCompute.SetBuffer(_kernelSolveConstraints, "PhysicsData", _physicsBuffer);
    clothCompute.SetBuffer(_kernelSolveConstraints, "SpringLinks", _springLinksBuffer);
    clothCompute.SetBuffer(_kernelSolveConstraints, "Springs", _springsBuffer);
    clothCompute.SetBuffer(_kernelSolveConstraints, "Colliders", _collidersBuffer);

    // Bind Integrate (Including Adjacency fix for internal friction)
    clothCompute.SetBuffer(_kernelIntegrate, "Positions", _positionsBuffer);
    clothCompute.SetBuffer(_kernelIntegrate, "PredictedPositions", _predictedPositionsBuffer);
    clothCompute.SetBuffer(_kernelIntegrate, "PhysicsData", _physicsBuffer);
    clothCompute.SetBuffer(_kernelIntegrate, "Adjacency", _adjacencyBuffer); 

    // Bind Normals
    clothCompute.SetBuffer(_kernelComputeNormals, "Positions", _positionsBuffer);
    clothCompute.SetBuffer(_kernelComputeNormals, "Adjacency", _adjacencyBuffer);
    clothCompute.SetBuffer(_kernelComputeNormals, "Normals", _normalsBuffer);

    // Bind to Render Material
    clothMaterial.SetBuffer("_PositionsBuffer", _positionsBuffer);
    clothMaterial.SetBuffer("_NormalsBuffer", _normalsBuffer);
    clothMaterial.SetBuffer("_UVsBuffer", _uvsBuffer);
}

        // ==========================================
        // --- STRUCT DEFINITIONS (Must match HLSL) ---
        // ==========================================

        [StructLayout(LayoutKind.Sequential)]
        private struct PhysicsState
        {
            public Vector3 velocity;
            public float inverseMass;
            public uint colliderStart;
            public uint colliderCount;
            public float selfCollideMask;
            public float padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SpringLink 
        { 
            public uint startIndex; 
            public uint count; 
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Spring
        {
            public uint targetIndex;
            public float restLength;
            public float compliance;
            public float padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Int4 
        { 
            public int x, y, z, w; 
            public Int4(int _x, int _y, int _z, int _w) { x = _x; y = _y; z = _z; w = _w; } 
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ClothColliderData
        {
            public int type;
            public float radius;
            public Vector2 padding1;

            public Vector3 posA;
            public float padding2;

            public Vector3 posB;
            public float padding3;

            public Matrix4x4 worldToLocal;
            public Matrix4x4 localToWorld;

            public Vector3 extents;
            public float padding4;
        }
    }
}