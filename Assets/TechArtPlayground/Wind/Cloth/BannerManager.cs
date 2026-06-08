using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace TechArtPlayground.Wind.Cloth
{
    [DefaultExecutionOrder(-50)]
    public class PhysicsBannerManager : MonoBehaviour
    {
        private const float FixedTimeStep = 0.01666f;

        // --- Shader Properties ---
        private static readonly int WindVelocityID = Shader.PropertyToID("_WindVelocity");
        private static readonly int WindTurbulenceID = Shader.PropertyToID("_WindTurbulence");
        private static readonly int DeltaTimeID = Shader.PropertyToID("_DeltaTime");
        private static readonly int TimeID = Shader.PropertyToID("_Time");
        private static readonly int GravityID = Shader.PropertyToID("_Gravity");
        private static readonly int DragID = Shader.PropertyToID("_Drag");
        private static readonly int CellSizeID = Shader.PropertyToID("_CellSize");
        private static readonly int SelfCollisionThicknessID = Shader.PropertyToID("_SelfCollisionThickness");
        private static readonly int HashGridSizeID = Shader.PropertyToID("_HashGridSize");

        // Compute Buffer IDs 
        private static readonly int PredictedPositions = Shader.PropertyToID("PredictedPositions");
        private static readonly int HashPairs = Shader.PropertyToID("HashPairs");
        private static readonly int CellOffsets = Shader.PropertyToID("CellOffsets");
        private static readonly int PhysicsData = Shader.PropertyToID("PhysicsData");
        private static readonly int Positions = Shader.PropertyToID("Positions");
        private static readonly int Normals = Shader.PropertyToID("Normals");
        private static readonly int SpringLinks = Shader.PropertyToID("SpringLinks");
        private static readonly int Springs = Shader.PropertyToID("Springs");
        private static readonly int Colliders = Shader.PropertyToID("Colliders");
        private static readonly int Adjacency = Shader.PropertyToID("Adjacency");
        private static readonly int VertexCount = Shader.PropertyToID("_VertexCount");
        private static readonly int SortedHashPairs = Shader.PropertyToID("SortedHashPairs");
        private static readonly int GlobalHist = Shader.PropertyToID("GlobalHist");
        private static readonly int NumElements = Shader.PropertyToID("numElements");
        private static readonly int NumBlocks = Shader.PropertyToID("numBlocks");
        private static readonly int LocalOffsets = Shader.PropertyToID("LocalOffsets");
        private static readonly int BITShift = Shader.PropertyToID("bitShift");
        private static readonly int InputBuffer = Shader.PropertyToID("InputBuffer");
        private static readonly int OutputBuffer = Shader.PropertyToID("OutputBuffer");
        private static readonly int BufferID = Shader.PropertyToID("_NormalsBuffer");
        private static readonly int PositionsBufferID = Shader.PropertyToID("_PositionsBuffer");
        private static readonly int VsBuffer = Shader.PropertyToID("_UVsBuffer");
        private static readonly int TrianglesID = Shader.PropertyToID("Triangles");
        private static readonly int VertexTriLinksID = Shader.PropertyToID("VertexTriLinks");
        private static readonly int VertexTriIndicesID = Shader.PropertyToID("VertexTriIndices");

        [Header("Resources")] 
        public ComputeShader clothCompute;
        public Material clothMaterial;
        public ComputeShader radixSortCompute;

        // ----------------------------------------------------
        // 1. INSPECTOR & ENCAPSULATED PROPERTIES
        // ----------------------------------------------------
        [Header("XPBD Physics Settings")] 
        [SerializeField] private Vector3 gravity = new(0, -9.81f, 0);
        public Vector3 Gravity 
        { 
            get => gravity; 
            set { if (gravity == value) return; gravity = value; PushVector(GravityID, value); } 
        }

        [Range(0f, 15f)] [SerializeField] private float drag = 2.5f;
        public float Drag 
        { 
            get => drag; 
            set { if (Mathf.Approximately(drag, value)) return; drag = value; PushFloat(DragID, value); } 
        }

        [SerializeField] private float springCompliance = 0.001f; // Initialized per instance, no dynamic push needed

        [Range(1, 40)] [SerializeField] private int solverIterations = 20;
        public int SolverIterations 
        { 
            get => solverIterations; 
            set 
            { 
                if (solverIterations == value) return; 
                solverIterations = value; 
                // Sub-step delta pre-calculation pushed instantly to shader
                float subStepDelta = FixedTimeStep / Mathf.Max(1, solverIterations);
                PushFloat(DeltaTimeID, subStepDelta); 
            } 
        }

        [Header("Optimization & Culling")] 
        public float sleepDelay = 2.0f;

        [Header("Self Collision")] 
        public bool enableSelfCollision = true;

        [SerializeField] private float clothThickness = 0.05f;
        public float ClothThickness 
        { 
            get => clothThickness; 
            set { if (Mathf.Approximately(clothThickness, value)) return; clothThickness = value; PushFloat(SelfCollisionThicknessID, value); } 
        }

        [SerializeField] private float spatialCellSize = 0.1f;
        public float SpatialCellSize 
        { 
            get => spatialCellSize; 
            set { if (Mathf.Approximately(spatialCellSize, value)) return; spatialCellSize = value; PushFloat(CellSizeID, value); } 
        }

        [SerializeField] private int hashGridSize = 8192;

        // Hidden dynamic weather properties
        private Vector3 _windVelocity;
        public Vector3 WindVelocity 
        { 
            get => _windVelocity; 
            set { if (_windVelocity == value) return; _windVelocity = value; PushVector(WindVelocityID, value); } 
        }

        private float _windTurbulence;
        public float WindTurbulence 
        { 
            get => _windTurbulence; 
            set { if (Mathf.Approximately(_windTurbulence, value)) return; _windTurbulence = value; PushFloat(WindTurbulenceID, value); } 
        }

        // --- Core Systems ---
        private readonly List<BannerInstance> _bannerInstances = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];

        private int _kHash, _kClearOffsets, _kBuildOffsets, _kSelfCollide;
        private int _kPredict, _kSolve, _kIntegrate, _kNormals;
        private Camera _mainCam;

        private GraphicsBuffer _sharedGlobalHistBuffer;
        private GraphicsBuffer _sharedLocalOffsetsBuffer;

        private float _timeAccumulator;

        private void OnEnable()
        {
            _mainCam = Camera.main;

            InitializeKernels();
            InitializeBanners();
            PushAllComputeData();
        }

        private void OnDisable()
        {
            foreach (BannerInstance inst in _bannerInstances) inst.Dispose();
            _sharedGlobalHistBuffer?.Dispose();
            _sharedLocalOffsetsBuffer?.Dispose();
            _bannerInstances.Clear();
        }

        private void OnDestroy()
        {
            foreach (BannerInstance inst in _bannerInstances) inst.Dispose();
            _sharedGlobalHistBuffer?.Dispose();
            _sharedLocalOffsetsBuffer?.Dispose();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            PushAllComputeData();
        }
#endif

        private void PushFloat(int id, float val) { if (clothCompute != null) clothCompute.SetFloat(id, val); }
        private void PushInt(int id, int val) { if (clothCompute != null) clothCompute.SetInt(id, val); }
        private void PushVector(int id, Vector3 val) { if (clothCompute != null) clothCompute.SetVector(id, val); }

        private void PushAllComputeData()
        {
            PushVector(GravityID, gravity);
            PushFloat(DragID, drag);
            PushFloat(SelfCollisionThicknessID, clothThickness);
            PushFloat(CellSizeID, spatialCellSize);
            PushVector(WindVelocityID, _windVelocity);
            PushFloat(WindTurbulenceID, _windTurbulence);
            PushInt(HashGridSizeID, hashGridSize);

            // Force initial sub-step delta push
            float subStepDelta = FixedTimeStep / Mathf.Max(1, solverIterations);
            PushFloat(DeltaTimeID, subStepDelta);
        }

        private void Update()
        {
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null) return; 

            if (_bannerInstances.Count == 0) return;
            
            // 1. Wind Polling Firewall
            if (GlobalWeatherManager.Instance != null)
            {
                // Properties will halt execution here if weather hasn't shifted
                WindVelocity = GlobalWeatherManager.Instance.CurrentWindVelocity;
                WindTurbulence = GlobalWeatherManager.Instance.CurrentWindTurbulence;
            }

            // 2. Frustum Culling Check
            GeometryUtility.CalculateFrustumPlanes(_mainCam, _frustumPlanes);

            foreach (BannerInstance inst in _bannerInstances)
            {
                inst.IsVisible = GeometryUtility.TestPlanesAABB(_frustumPlanes, inst.WorldBounds);

                if (inst.IsVisible)
                {
                    inst.TimeSinceVisible = 0f;
                    inst.IsActive = true;
                }
                else
                {
                    inst.TimeSinceVisible += Time.unscaledDeltaTime;
                    inst.IsActive = inst.TimeSinceVisible < sleepDelay;
                }
            }

            // 3. Fixed Timestep Physics Loop
            _timeAccumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            while (_timeAccumulator >= FixedTimeStep)
            {
                StepSimulation();
                _timeAccumulator -= FixedTimeStep;
            }

            // 4. Render Loop

            foreach (BannerInstance inst in _bannerInstances)
            {
                if (inst.IsVisible)
                {
                    // CHANGED: Passed 'null' instead of '_mainCam' to allow URP Shadow rendering
                    Graphics.DrawMesh(
                        inst.RenderMesh, 
                        Matrix4x4.identity, 
                        inst.Node.clothMaterial, 
                        gameObject.layer, 
                        null, 
                        0, 
                        inst.MatBlock
                    );
                }
            }
        }

        // ==========================================
        // PUBLIC API: ON-DEMAND COLLIDER REFRESH
        // ==========================================
        public void RefreshCollidersFor(PhysicsBannerNode targetNode)
        {
            foreach (BannerInstance inst in _bannerInstances)
                if (inst.Node == targetNode)
                {
                    inst.UpdateDynamicColliders();
                    return;
                }
        }

        private void StepSimulation()
        {
            // Only Continuous Time needs to be updated per tick.
            clothCompute.SetFloat(TimeID, Time.unscaledTime);

            int hashGroups = Mathf.CeilToInt(hashGridSize / 64f);

            foreach (BannerInstance inst in _bannerInstances)
            {
                if (!inst.IsActive) continue;

                int groupsX = Mathf.CeilToInt(inst.VertexCount / 64f);
                BindInstanceToCompute(inst);

                // --- SPATIAL HASHING ---
                if (enableSelfCollision)
                {
                    clothCompute.Dispatch(_kHash, groupsX, 1, 1);
                    DispatchRadixSort(inst);

                    clothCompute.Dispatch(_kClearOffsets, hashGroups, 1, 1);
                    clothCompute.Dispatch(_kBuildOffsets, groupsX, 1, 1);
                }

                // --- XPBD SUB-STEP SOLVER ---
                for (int i = 0; i < solverIterations; i++)
                {
                    clothCompute.Dispatch(_kPredict, groupsX, 1, 1);
                    clothCompute.Dispatch(_kSolve, groupsX, 1, 1);

                    if (enableSelfCollision) clothCompute.Dispatch(_kSelfCollide, groupsX, 1, 1);

                    clothCompute.Dispatch(_kIntegrate, groupsX, 1, 1);
                }

                clothCompute.Dispatch(_kNormals, groupsX, 1, 1);
            }
        }

        private void BindInstanceToCompute(BannerInstance inst)
        {
            clothCompute.SetInt(VertexCount, inst.VertexCount);

            // 1. Predict Kernel
            clothCompute.SetBuffer(_kPredict, Positions, inst.PositionsBuffer);
            clothCompute.SetBuffer(_kPredict, PredictedPositions, inst.PredictedPositionsBuffer);
            clothCompute.SetBuffer(_kPredict, PhysicsData, inst.PhysicsBuffer);
            clothCompute.SetBuffer(_kPredict, Normals, inst.NormalsBuffer);

            // 2. Solve Constraints Kernel
            clothCompute.SetBuffer(_kSolve, PredictedPositions, inst.PredictedPositionsBuffer);
            clothCompute.SetBuffer(_kSolve, PhysicsData, inst.PhysicsBuffer);
            clothCompute.SetBuffer(_kSolve, SpringLinks, inst.SpringLinksBuffer);
            clothCompute.SetBuffer(_kSolve, Springs, inst.SpringsBuffer);
            clothCompute.SetBuffer(_kSolve, Colliders, inst.CollidersBuffer);

            // 3. Self Collide Kernel (Only bounds if active)
            if (enableSelfCollision)
            {
                clothCompute.SetBuffer(_kSelfCollide, Positions, inst.PositionsBuffer);
                clothCompute.SetBuffer(_kSelfCollide, PredictedPositions, inst.PredictedPositionsBuffer);
                clothCompute.SetBuffer(_kSelfCollide, PhysicsData, inst.PhysicsBuffer);
                clothCompute.SetBuffer(_kSelfCollide, CellOffsets, inst.CellOffsetsBuffer);
                clothCompute.SetBuffer(_kSelfCollide, SortedHashPairs, inst.SortedHashPairsBuffer);
            }

            // 4. Integrate Kernel (UPDATED for Arbitrary Springs)
            clothCompute.SetBuffer(_kIntegrate, Positions, inst.PositionsBuffer);
            clothCompute.SetBuffer(_kIntegrate, PredictedPositions, inst.PredictedPositionsBuffer);
            clothCompute.SetBuffer(_kIntegrate, PhysicsData, inst.PhysicsBuffer);
            clothCompute.SetBuffer(_kIntegrate, SpringLinks, inst.SpringLinksBuffer); // Replaced Adjacency
            clothCompute.SetBuffer(_kIntegrate, Springs, inst.SpringsBuffer);         // Replaced Adjacency

            // 5. Normals Kernel (UPDATED for Arbitrary Triangles)
            clothCompute.SetBuffer(_kNormals, Positions, inst.PositionsBuffer);
            clothCompute.SetBuffer(_kNormals, Normals, inst.NormalsBuffer);
            clothCompute.SetBuffer(_kNormals, TrianglesID, inst.TrianglesBuffer);               // New Topology
            clothCompute.SetBuffer(_kNormals, VertexTriLinksID, inst.VertexTriLinksBuffer);     // New Topology
            clothCompute.SetBuffer(_kNormals, VertexTriIndicesID, inst.VertexTriIndicesBuffer); // New Topology

            // 6. Hashing Kernels (Unchanged)
            if (enableSelfCollision)
            {
                clothCompute.SetBuffer(_kHash, Positions, inst.PositionsBuffer);
                clothCompute.SetBuffer(_kHash, HashPairs, inst.HashPairsBuffer);
                clothCompute.SetBuffer(_kClearOffsets, CellOffsets, inst.CellOffsetsBuffer);
                clothCompute.SetBuffer(_kBuildOffsets, CellOffsets, inst.CellOffsetsBuffer);
                clothCompute.SetBuffer(_kBuildOffsets, SortedHashPairs, inst.SortedHashPairsBuffer);
            }
        }

        private void DispatchRadixSort(BannerInstance inst)
        {
            int numElements = inst.VertexCount;
            int numBlocks = Mathf.CeilToInt(numElements / 256f);

            radixSortCompute.SetInt(NumElements, numElements);
            radixSortCompute.SetInt(NumBlocks, numBlocks);

            radixSortCompute.SetBuffer(0, GlobalHist, _sharedGlobalHistBuffer);
            radixSortCompute.SetBuffer(0, LocalOffsets, _sharedLocalOffsetsBuffer);
            radixSortCompute.SetBuffer(1, GlobalHist, _sharedGlobalHistBuffer);

            GraphicsBuffer input = inst.HashPairsBuffer;
            GraphicsBuffer output = inst.SortedHashPairsBuffer;

            for (int shift = 0; shift < 32; shift += 4)
            {
                radixSortCompute.SetInt(BITShift, shift);
                radixSortCompute.SetBuffer(0, InputBuffer, input);
                radixSortCompute.SetBuffer(2, InputBuffer, input);
                radixSortCompute.SetBuffer(2, OutputBuffer, output);
                radixSortCompute.SetBuffer(2, LocalOffsets, _sharedLocalOffsetsBuffer);
                radixSortCompute.SetBuffer(2, GlobalHist, _sharedGlobalHistBuffer);

                radixSortCompute.Dispatch(0, numBlocks, 1, 1);
                radixSortCompute.Dispatch(1, 16, 1, 1);
                radixSortCompute.Dispatch(2, numBlocks, 1, 1);

                (input, output) = (output, input); // Ping-pong
            }
        }

        private void InitializeKernels()
        {
            _kHash = clothCompute.FindKernel("CSHashParticles");
            _kClearOffsets = clothCompute.FindKernel("CSClearCellOffsets");
            _kBuildOffsets = clothCompute.FindKernel("CSBuildCellOffsets");
            _kSelfCollide = clothCompute.FindKernel("CSSolveSelfCollisions");
            _kPredict = clothCompute.FindKernel("CSPredict");
            _kSolve = clothCompute.FindKernel("CSSolveConstraints");
            _kIntegrate = clothCompute.FindKernel("CSIntegrate");
            _kNormals = clothCompute.FindKernel("CSComputeNormals");
        }

        private void InitializeBanners()
        {
            PhysicsBannerNode[] nodes = FindObjectsByType<PhysicsBannerNode>(FindObjectsInactive.Exclude);
            int maxVerticesInAnyBanner = 0;

            foreach (PhysicsBannerNode node in nodes)
            {
                BannerInstance inst = new(node, springCompliance, hashGridSize);
                _bannerInstances.Add(inst);

                if (inst.VertexCount > maxVerticesInAnyBanner)
                    maxVerticesInAnyBanner = inst.VertexCount;
            }

            // Allocate shared Radix Sort buffers scaled only to the single largest banner
            int maxBlocks = Mathf.Max(1, Mathf.CeilToInt(maxVerticesInAnyBanner / 256f));
            _sharedGlobalHistBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 16 * maxBlocks, 4);
            _sharedLocalOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxVerticesInAnyBanner, 4);
        }

        // ==========================================
        // STRUCTS
        // ==========================================
        
        [StructLayout(LayoutKind.Sequential)]
        public struct Int3 { public int x, y, z; }

        [StructLayout(LayoutKind.Sequential)]
        public struct VertexTriangleLink { public uint startIndex; public uint count; }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct PhysicsState
        {
            public Vector3 velocity;
            public float inverseMass;
            public uint colliderStart;
            public uint colliderCount;
            public float selfCollideMask;
            public float padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SpringLink
        {
            public uint startIndex;
            public uint count;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Spring
        {
            public uint targetIndex;
            public float restLength;
            public float compliance;
            public float padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Int4
        {
            public int x, y, z, w;

            public Int4(int _x, int _y, int _z, int _w)
            {
                x = _x;
                y = _y;
                z = _z;
                w = _w;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ClothColliderData
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

        // ==========================================
        // THE CHUNK CLASS
        // ==========================================
        private class BannerInstance
        {
            private readonly ClothStaticCollider[] _activeColliders;
            private readonly ClothColliderData[] _colliderDataArray;
            public readonly GraphicsBuffer AdjacencyBuffer;
            public readonly GraphicsBuffer CellOffsetsBuffer;
            public readonly GraphicsBuffer CollidersBuffer;
            public readonly GraphicsBuffer HashPairsBuffer;
            
            
            public bool IsActive = true;
            public bool IsVisible = true;
            public readonly MaterialPropertyBlock MatBlock;
            public readonly PhysicsBannerNode Node;
            public readonly GraphicsBuffer NormalsBuffer;
            public readonly GraphicsBuffer PhysicsBuffer;

            // Buffers Isolated per Banner
            public readonly GraphicsBuffer PositionsBuffer;
            public readonly GraphicsBuffer PredictedPositionsBuffer;
            public readonly Mesh RenderMesh;
            public readonly GraphicsBuffer SortedHashPairsBuffer;
            public readonly GraphicsBuffer SpringLinksBuffer;
            public readonly GraphicsBuffer SpringsBuffer;
            public float TimeSinceVisible;
            public readonly GraphicsBuffer UVsBuffer;
            
            public readonly GraphicsBuffer TrianglesBuffer;
            public readonly GraphicsBuffer VertexTriLinksBuffer;
            public readonly GraphicsBuffer VertexTriIndicesBuffer;

            public readonly int VertexCount;
            public readonly Bounds WorldBounds;

            public BannerInstance(PhysicsBannerNode node, float compliance, int hashSize)
            {
                Node = node;
                Mesh sourceMesh = node.clothMesh;
                VertexCount = sourceMesh.vertexCount;
                
                // Fallback if bounds aren't calculated
                WorldBounds = new Bounds(node.transform.position, sourceMesh.bounds.size * 2f);
                _activeColliders = node.GetComponentsInChildren<ClothStaticCollider>();
                _colliderDataArray = new ClothColliderData[Mathf.Max(1, _activeColliders.Length)];

                Vector3[] verts = sourceMesh.vertices;
                Vector3[] norms = sourceMesh.normals;
                Vector2[] uvs = sourceMesh.uv;
                Color[] colors = sourceMesh.colors;
                int[] triangles = sourceMesh.triangles;

                // Fallback if mesh has no vertex colors
                if (colors == null || colors.Length == 0)
                {
                    colors = new Color[VertexCount];
                    for (int i = 0; i < VertexCount; i++) colors[i] = Color.white;
                }

                List<PhysicsState> physics = new List<PhysicsState>(VertexCount);
                for (int i = 0; i < VertexCount; i++)
                {
                    Vector3 worldPos = node.transform.TransformPoint(verts[i]);
                    verts[i] = worldPos; // Convert to world space for initial state

                    // Extract Physics data from Vertex Colors
                    float invMass = colors[i].r;
                    float stiffnessMult = Mathf.Lerp(1.0f, 0.0f, colors[i].g);
                    float selfCollide = colors[i].b;

                    physics.Add(new PhysicsState
                    {
                        velocity = Vector3.zero,
                        inverseMass = invMass,
                        colliderStart = 0,
                        colliderCount = (uint)_activeColliders.Length,
                        selfCollideMask = selfCollide,
                        padding = 0f
                    });
                }

                // --- GENERATE ARBITRARY SPRINGS FROM EDGES ---
                List<SpringLink> springLinks = new List<SpringLink>(VertexCount);
                List<Spring> springs = new List<Spring>();
                
                // Build an adjacency list to find unique connections per vertex
                List<int>[] vertexNeighbors = new List<int>[VertexCount];
                for (int i = 0; i < VertexCount; i++) vertexNeighbors[i] = new List<int>();

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int v0 = triangles[i];
                    int v1 = triangles[i + 1];
                    int v2 = triangles[i + 2];

                    if (!vertexNeighbors[v0].Contains(v1)) vertexNeighbors[v0].Add(v1);
                    if (!vertexNeighbors[v0].Contains(v2)) vertexNeighbors[v0].Add(v2);
                    
                    if (!vertexNeighbors[v1].Contains(v0)) vertexNeighbors[v1].Add(v0);
                    if (!vertexNeighbors[v1].Contains(v2)) vertexNeighbors[v1].Add(v2);
                    
                    if (!vertexNeighbors[v2].Contains(v0)) vertexNeighbors[v2].Add(v0);
                    if (!vertexNeighbors[v2].Contains(v1)) vertexNeighbors[v2].Add(v1);
                }

                for (int i = 0; i < VertexCount; i++)
                {
                    uint startIndex = (uint)springs.Count;
                    foreach (int neighbor in vertexNeighbors[i])
                    {
                        float dist = Vector3.Distance(verts[i], verts[neighbor]);
                        float stiffnessMult = Mathf.Lerp(1.0f, 0.0f, colors[i].g);
                        
                        springs.Add(new Spring
                        {
                            targetIndex = (uint)neighbor,
                            restLength = dist,
                            compliance = compliance * stiffnessMult,
                            padding = 0
                        });
                    }
                    springLinks.Add(new SpringLink { startIndex = startIndex, count = (uint)vertexNeighbors[i].Count });
                }

                // --- GENERATE NORMALS TOPOLOGY ---
                // We map which triangles belong to which vertex to calculate parallel face normals
                List<Int3> computeTriangles = new List<Int3>();
                for (int i = 0; i < triangles.Length; i += 3)
                    computeTriangles.Add(new Int3 { x = triangles[i], y = triangles[i+1], z = triangles[i+2] });

                List<int>[] vertexToTriangleMap = new List<int>[VertexCount];
                for (int i = 0; i < VertexCount; i++) vertexToTriangleMap[i] = new List<int>();
                for (int i = 0; i < computeTriangles.Count; i++)
                {
                    vertexToTriangleMap[computeTriangles[i].x].Add(i);
                    vertexToTriangleMap[computeTriangles[i].y].Add(i);
                    vertexToTriangleMap[computeTriangles[i].z].Add(i);
                }

                List<VertexTriangleLink> triLinks = new List<VertexTriangleLink>(VertexCount);
                List<int> triIndices = new List<int>();
                for (int i = 0; i < VertexCount; i++)
                {
                    triLinks.Add(new VertexTriangleLink { startIndex = (uint)triIndices.Count, count = (uint)vertexToTriangleMap[i].Count });
                    triIndices.AddRange(vertexToTriangleMap[i]);
                }

                // Create the Local Render Mesh
                RenderMesh = new Mesh { name = "BannerMesh_" + node.name, indexFormat = IndexFormat.UInt32 };
                RenderMesh.SetVertices(verts);
                RenderMesh.SetIndices(triangles, MeshTopology.Triangles, 0);
                RenderMesh.SetUVs(0, uvs);
                RenderMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

                // 4. Allocate Independent GPU Buffers
                PositionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, VertexCount, 12);
                PredictedPositionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, VertexCount, 12);
                NormalsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, VertexCount, 12);
                UVsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, VertexCount, 8);
                PhysicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, VertexCount, 32);
                SpringLinksBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, VertexCount, 8);
                SpringsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, springs.Count, 16);
                AdjacencyBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, VertexCount, 16);
                HashPairsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, VertexCount, 8);
                SortedHashPairsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, VertexCount, 8);
                CellOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, hashSize, 4);
                CollidersBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, _activeColliders.Length), 192);

                TrianglesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, computeTriangles.Count, 12);
                VertexTriLinksBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, VertexCount, 8);
                VertexTriIndicesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, triIndices.Count, 4);
                
                PositionsBuffer.SetData(verts);
                PredictedPositionsBuffer.SetData(verts); // <--- Added: Prevents NaN explosion on Frame 1 physics
                NormalsBuffer.SetData(norms);            // <--- Added: Initializes starting normals
                UVsBuffer.SetData(uvs);

                PositionsBuffer.SetData(verts);
                PhysicsBuffer.SetData(physics);
                SpringLinksBuffer.SetData(springLinks);
                SpringsBuffer.SetData(springs);
                TrianglesBuffer.SetData(computeTriangles);
                VertexTriLinksBuffer.SetData(triLinks);
                VertexTriIndicesBuffer.SetData(triIndices);

                UpdateDynamicColliders();

                MatBlock = new MaterialPropertyBlock();
                MatBlock.SetBuffer(PositionsBufferID, PositionsBuffer);
                MatBlock.SetBuffer(BufferID, NormalsBuffer);
                MatBlock.SetBuffer(VsBuffer, UVsBuffer);
            }

            public void UpdateDynamicColliders()
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
                        float h = Mathf.Max(0, c.height * 0.5f - c.radius);
                        _colliderDataArray[i].posA = c.transform.position + up * h;
                        _colliderDataArray[i].posB = c.transform.position - up * h;
                    }
                    else if (c.colliderType == ClothColliderType.Box)
                    {
                        _colliderDataArray[i].worldToLocal = c.transform.worldToLocalMatrix;
                        _colliderDataArray[i].localToWorld = c.transform.localToWorldMatrix;
                        _colliderDataArray[i].extents = c.boxExtents;
                    }
                }

                CollidersBuffer.SetData(_colliderDataArray);
            }

            public void Dispose()
            {
                PositionsBuffer?.Dispose();
                PredictedPositionsBuffer?.Dispose();
                NormalsBuffer?.Dispose();
                UVsBuffer?.Dispose();
                PhysicsBuffer?.Dispose();
                SpringLinksBuffer?.Dispose();
                SpringsBuffer?.Dispose();
                AdjacencyBuffer?.Dispose(); // Keep this if you haven't fully deleted it yet
                HashPairsBuffer?.Dispose();
                SortedHashPairsBuffer?.Dispose();
                CellOffsetsBuffer?.Dispose();
                CollidersBuffer?.Dispose();
    
                // Add these three new lines:
                TrianglesBuffer?.Dispose();
                VertexTriLinksBuffer?.Dispose();
                VertexTriIndicesBuffer?.Dispose();
            }
        }
    }
}