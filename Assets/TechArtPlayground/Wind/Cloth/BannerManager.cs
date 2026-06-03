using System.Collections.Generic;
using System.Runtime.InteropServices;
using TechArtPlayground.Wind;
using TechArtPlayground.Wind.Cloth;
using UnityEngine;
using UnityEngine.Rendering;

namespace TechArtPlayground.Cloth
{
    public class PhysicsBannerManager : MonoBehaviour
    {
        // --- Shader Properties ---
        private static readonly int WindVelocity = Shader.PropertyToID("_WindVelocity");
        private static readonly int WindTurbulence = Shader.PropertyToID("_WindTurbulence");
        private static readonly int DeltaTime = Shader.PropertyToID("_DeltaTime");
        private static readonly int Time1 = Shader.PropertyToID("_Time");
        private static readonly int Gravity = Shader.PropertyToID("_Gravity");
        private static readonly int Drag = Shader.PropertyToID("_Drag");
        private static readonly int CellSize = Shader.PropertyToID("_CellSize");
        private static readonly int SelfCollisionThickness = Shader.PropertyToID("_SelfCollisionThickness");
        private static readonly int HashGridSize = Shader.PropertyToID("_HashGridSize");
        
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
        private static readonly int Buffer = Shader.PropertyToID("_NormalsBuffer");
        private static readonly int PositionsBuffer1 = Shader.PropertyToID("_PositionsBuffer");
        private static readonly int VsBuffer = Shader.PropertyToID("_UVsBuffer");

        [Header("Resources")]
        public ComputeShader clothCompute;
        public Material clothMaterial;
        public ComputeShader radixSortCompute;

        [Header("XPBD Physics Settings")]
        public Vector3 gravity = new(0, -9.81f, 0);
        [Range(0f, 15f)] public float drag = 2.5f;
        public float springCompliance = 0.001f; 
        [Range(1, 40)] public int solverIterations = 20;
        
        [Header("Optimization & Culling")]
        [Tooltip("How many seconds the cloth continues to simulate after leaving the camera view before freezing.")]
        public float sleepDelay = 2.0f;

        [Header("Self Collision")]
        public bool enableSelfCollision = true;
        public float clothThickness = 0.05f;
        public float spatialCellSize = 0.1f;
        public int hashGridSize = 8192; // Reduced drastically! It's now localized per-banner.

        // --- Kernels ---
        private int _kHash, _kClearOffsets, _kBuildOffsets, _kSelfCollide;
        private int _kPredict, _kSolve, _kIntegrate, _kNormals;

        // --- CHUNKING: The Banner Instances ---
        private List<BannerInstance> _bannerInstances = new();
        private Camera _mainCam;
        private Plane[] _frustumPlanes = new Plane[6];

        // --- Shared Temporary Buffers (Memory Optimization) ---
        // We reuse these for Radix Sort across all banners to save VRAM
        private GraphicsBuffer _sharedGlobalHistBuffer;
        private GraphicsBuffer _sharedLocalOffsetsBuffer;

        private float _timeAccumulator = 0.0f;
        private const float FixedTimeStep = 0.01666f;

        private void Start()
        {
            _mainCam = Camera.main;
            InitializeKernels();
            InitializeBanners();
        }

        private void Update()
        {
            if (_bannerInstances.Count == 0) return;

            // 1. Frustum Culling Check
            GeometryUtility.CalculateFrustumPlanes(_mainCam, _frustumPlanes);

            foreach (var inst in _bannerInstances)
            {
                // STEP 2: PHYSICS SLEEP STATES
                inst.UpdateDynamicColliders(); // Update colliders associated specifically with this banner
                
                // Test bounds against camera
                inst.IsVisible = GeometryUtility.TestPlanesAABB(_frustumPlanes, inst.WorldBounds);

                if (inst.IsVisible)
                {
                    inst.TimeSinceVisible = 0f;
                    inst.IsActive = true;
                }
                else
                {
                    inst.TimeSinceVisible += Time.unscaledDeltaTime;
                    // Freeze simulation if off-screen for longer than the sleep delay
                    inst.IsActive = inst.TimeSinceVisible < sleepDelay; 
                }
            }

            // 2. Fixed Timestep Physics Loop (Only processes Active banners)
            _timeAccumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            while (_timeAccumulator >= FixedTimeStep)
            {
                StepSimulation(FixedTimeStep);
                _timeAccumulator -= FixedTimeStep;
            }

            // 3. Render Loop (Only renders Visible banners)
            foreach (var inst in _bannerInstances)
            {
                if (inst.IsVisible)
                {
                    Graphics.DrawMesh(inst.RenderMesh, Matrix4x4.identity, clothMaterial, gameObject.layer, _mainCam, 0, inst.MatBlock);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var inst in _bannerInstances) inst.Dispose();
            _sharedGlobalHistBuffer?.Dispose();
            _sharedLocalOffsetsBuffer?.Dispose();
        }

        private void StepSimulation(float dt)
        {
            float subStepDelta = dt / solverIterations;

            // Global Shader Variables
            clothCompute.SetFloat(DeltaTime, subStepDelta); 
            clothCompute.SetFloat(Time1, Time.unscaledTime);
            clothCompute.SetVector(Gravity, gravity);
            clothCompute.SetFloat(Drag, drag);
            clothCompute.SetFloat(CellSize, spatialCellSize);
            clothCompute.SetFloat(SelfCollisionThickness, clothThickness);
            clothCompute.SetInt(HashGridSize, hashGridSize);

            // Mocking Global Wind for standalone use
// Restore dynamic weather linkage with a safe fallback
            if (GlobalWeatherManager.Instance != null)
            {
                clothCompute.SetVector(WindVelocity, GlobalWeatherManager.Instance.CurrentWindVelocity);
                clothCompute.SetFloat(WindTurbulence, GlobalWeatherManager.Instance.CurrentWindTurbulence);
            }
            else
            {
                // Fallback if the weather manager isn't loaded yet
                clothCompute.SetVector(WindVelocity, new Vector3(2f, 0f, 5f));
                clothCompute.SetFloat(WindTurbulence, 0.5f);
            }

            int hashGroups = Mathf.CeilToInt(hashGridSize / 64f);

            // ==========================================
            // STEP 1: CHUNKING (Process Per-Instance)
            // ==========================================
            foreach (var inst in _bannerInstances)
            {
                if (!inst.IsActive) continue; // SKIPPED IF ASLEEP

                int groupsX = Mathf.CeilToInt(inst.VertexCount / 64f);
                BindInstanceToCompute(inst); // Points compute shader to this specific banner's buffers

                // --- SPATIAL HASHING (Localized) ---
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

                // Update Normals at the end of the full timestep
                clothCompute.Dispatch(_kNormals, groupsX, 1, 1);
            }
        }

        private void BindInstanceToCompute(BannerInstance inst)
        {
            clothCompute.SetInt(VertexCount, inst.VertexCount);

            // Predict Kernel
            clothCompute.SetBuffer(_kPredict, Positions, inst.PositionsBuffer);
            clothCompute.SetBuffer(_kPredict, PredictedPositions, inst.PredictedPositionsBuffer);
            clothCompute.SetBuffer(_kPredict, PhysicsData, inst.PhysicsBuffer);
            clothCompute.SetBuffer(_kPredict, Normals, inst.NormalsBuffer);

            // Solve Constraints Kernel
            clothCompute.SetBuffer(_kSolve, PredictedPositions, inst.PredictedPositionsBuffer);
            clothCompute.SetBuffer(_kSolve, PhysicsData, inst.PhysicsBuffer);
            clothCompute.SetBuffer(_kSolve, SpringLinks, inst.SpringLinksBuffer);
            clothCompute.SetBuffer(_kSolve, Springs, inst.SpringsBuffer);
            clothCompute.SetBuffer(_kSolve, Colliders, inst.CollidersBuffer);

            // Self Collide Kernel
            clothCompute.SetBuffer(_kSelfCollide, Positions, inst.PositionsBuffer);
            clothCompute.SetBuffer(_kSelfCollide, PredictedPositions, inst.PredictedPositionsBuffer);
            clothCompute.SetBuffer(_kSelfCollide, PhysicsData, inst.PhysicsBuffer);
            clothCompute.SetBuffer(_kSelfCollide, CellOffsets, inst.CellOffsetsBuffer);
            clothCompute.SetBuffer(_kSelfCollide, SortedHashPairs, inst.SortedHashPairsBuffer);

            // Integrate Kernel
            clothCompute.SetBuffer(_kIntegrate, Positions, inst.PositionsBuffer);
            clothCompute.SetBuffer(_kIntegrate, PredictedPositions, inst.PredictedPositionsBuffer);
            clothCompute.SetBuffer(_kIntegrate, PhysicsData, inst.PhysicsBuffer);
            clothCompute.SetBuffer(_kIntegrate, Adjacency, inst.AdjacencyBuffer); 

            // Normals Kernel
            clothCompute.SetBuffer(_kNormals, Positions, inst.PositionsBuffer);
            clothCompute.SetBuffer(_kNormals, Adjacency, inst.AdjacencyBuffer);
            clothCompute.SetBuffer(_kNormals, Normals, inst.NormalsBuffer);

            // Hashing Kernels
            clothCompute.SetBuffer(_kHash, Positions, inst.PositionsBuffer);
            clothCompute.SetBuffer(_kHash, HashPairs, inst.HashPairsBuffer);
            clothCompute.SetBuffer(_kClearOffsets, CellOffsets, inst.CellOffsetsBuffer);
            clothCompute.SetBuffer(_kBuildOffsets, CellOffsets, inst.CellOffsetsBuffer);
            clothCompute.SetBuffer(_kBuildOffsets, SortedHashPairs, inst.SortedHashPairsBuffer);
        }

        private void DispatchRadixSort(BannerInstance inst)
        {
            int numElements = inst.VertexCount;
            int numBlocks = Mathf.CeilToInt((float)numElements / 256f);

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
            PhysicsBannerNode[] nodes = FindObjectsByType<PhysicsBannerNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int maxVerticesInAnyBanner = 0;

            foreach (var node in nodes)
            {
                BannerInstance inst = new BannerInstance(node, springCompliance, hashGridSize);
                _bannerInstances.Add(inst);
                
                if (inst.VertexCount > maxVerticesInAnyBanner) 
                    maxVerticesInAnyBanner = inst.VertexCount;
            }

            // Allocate shared Radix Sort buffers scaled only to the single largest banner
            int maxBlocks = Mathf.Max(1, Mathf.CeilToInt((float)maxVerticesInAnyBanner / 256f));
            _sharedGlobalHistBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 16 * maxBlocks, 4);
            _sharedLocalOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxVerticesInAnyBanner, 4);
        }

        // ==========================================
        // STRUCTS
        // ==========================================
        [StructLayout(LayoutKind.Sequential)]
        public struct PhysicsState { public Vector3 velocity; public float inverseMass; public uint colliderStart; public uint colliderCount; public float selfCollideMask; public float padding; }
        [StructLayout(LayoutKind.Sequential)]
        public struct SpringLink { public uint startIndex; public uint count; }
        [StructLayout(LayoutKind.Sequential)]
        public struct Spring { public uint targetIndex; public float restLength; public float compliance; public float padding; }
        [StructLayout(LayoutKind.Sequential)]
        public struct Int4 { public int x, y, z, w; public Int4(int _x, int _y, int _z, int _w) { x = _x; y = _y; z = _z; w = _w; } }
        [StructLayout(LayoutKind.Sequential)]
        public struct ClothColliderData { public int type; public float radius; public Vector2 padding1; public Vector3 posA; public float padding2; public Vector3 posB; public float padding3; public Matrix4x4 worldToLocal; public Matrix4x4 localToWorld; public Vector3 extents; public float padding4; }

        // ==========================================
        // THE CHUNK CLASS
        // ==========================================
        private class BannerInstance
        {
            public PhysicsBannerNode Node;
            public Mesh RenderMesh;
            public Bounds WorldBounds;
            public MaterialPropertyBlock MatBlock;
            
            public int VertexCount;
            public bool IsActive = true;
            public bool IsVisible = true;
            public float TimeSinceVisible = 0f;

            // Buffers Isolated per Banner
            public GraphicsBuffer PositionsBuffer;
            public GraphicsBuffer PredictedPositionsBuffer;
            public GraphicsBuffer NormalsBuffer;
            public GraphicsBuffer UVsBuffer;
            public GraphicsBuffer PhysicsBuffer;
            public GraphicsBuffer SpringLinksBuffer;
            public GraphicsBuffer SpringsBuffer;
            public GraphicsBuffer AdjacencyBuffer;
            public GraphicsBuffer HashPairsBuffer;
            public GraphicsBuffer SortedHashPairsBuffer;
            public GraphicsBuffer CellOffsetsBuffer;
            public GraphicsBuffer CollidersBuffer;

            private ClothStaticCollider[] _activeColliders;
            private ClothColliderData[] _colliderDataArray;

            public BannerInstance(PhysicsBannerNode node, float compliance, int hashSize)
            {
                Node = node;
                VertexCount = node.resolution.x * node.resolution.y;

                // Generous bounding box to prevent culling when blowing in wind
                float maxStretch = Mathf.Max(node.dimensions.x, node.dimensions.y) * 2f;
                WorldBounds = new Bounds(node.transform.position, new Vector3(maxStretch, maxStretch, maxStretch));

                // 1. Gather Colliders specific to THIS node
                _activeColliders = node.GetComponentsInChildren<ClothStaticCollider>();
                _colliderDataArray = new ClothColliderData[Mathf.Max(1, _activeColliders.Length)];

                // 2. Generate Local Data Arrays
                List<Vector3> positions = new(VertexCount);
                List<Vector3> normals = new(VertexCount);
                List<Vector2> uvs = new(VertexCount);
                List<PhysicsState> physics = new(VertexCount);
                List<SpringLink> springLinks = new(VertexCount);
                List<Spring> springs = new();
                List<Int4> adjacency = new(VertexCount);
                List<int> meshIndices = new();

                Vector2 step = new(node.dimensions.x / (node.resolution.x - 1), node.dimensions.y / (node.resolution.y - 1));

                for (int y = 0; y < node.resolution.y; y++)
                {
                    for (int x = 0; x < node.resolution.x; x++)
                    {
                        int index = y * node.resolution.x + x;
                        Vector3 localPos = new(x * step.x - node.dimensions.x * 0.5f, -(y * step.y), 0);
                        positions.Add(node.transform.TransformPoint(localPos));
                        normals.Add(-node.transform.forward);
                        
                        float uCoord = (float)x / (node.resolution.x - 1);
                        float vCoord = 1.0f - (float)y / (node.resolution.y - 1);
                        uvs.Add(new Vector2(uCoord, vCoord));

                        float invMass = (y == 0) ? 0.0f : 1.0f;
                        float selfCollide = 1.0f; 

                        if (node.weightMap != null)
                        {
                            int texX = Mathf.Clamp(Mathf.RoundToInt(uCoord * (node.weightMap.width - 1)), 0, node.weightMap.width - 1);
                            int texY = Mathf.Clamp(Mathf.RoundToInt(vCoord * (node.weightMap.height - 1)), 0, node.weightMap.height - 1);
                            Color c = node.weightMap.GetPixel(texX, texY);
                            invMass = c.r;
                            selfCollide = c.b; 
                        }

                        physics.Add(new PhysicsState { velocity = Vector3.zero, inverseMass = invMass, colliderStart = 0, colliderCount = (uint)_activeColliders.Length, selfCollideMask = selfCollide, padding = 0f });

                        Int4 adj = new Int4(-1, -1, -1, -1);
                        if (x > 0) adj.x = index - 1;
                        if (x < node.resolution.x - 1) adj.y = index + 1;
                        if (y > 0) adj.z = index - node.resolution.x;
                        if (y < node.resolution.y - 1) adj.w = index + node.resolution.x;
                        
                        if (node.isPrayerFlagMode)
                        {
                            if (x > 0 && (x / node.flagWidth) != ((x - 1) / node.flagWidth)) adj.x = -1;
                            if (x < node.resolution.x - 1 && (x / node.flagWidth) != ((x + 1) / node.flagWidth)) adj.y = -1;
                        }
                        adjacency.Add(adj);
                    }
                }

                for (int y = 0; y < node.resolution.y; y++)
                {
                    for (int x = 0; x < node.resolution.x; x++)
                    {
                        int index = y * node.resolution.x + x;
                        uint startIndex = (uint)springs.Count;
                        uint springCount = 0;

                        float stiffnessMult = 1.0f;
                        if (node.weightMap != null)
                        {
                            int texX = Mathf.Clamp(Mathf.RoundToInt(((float)x / (node.resolution.x - 1)) * (node.weightMap.width - 1)), 0, node.weightMap.width - 1);
                            int texY = Mathf.Clamp(Mathf.RoundToInt((1.0f - (float)y / (node.resolution.y - 1)) * (node.weightMap.height - 1)), 0, node.weightMap.height - 1);
                            stiffnessMult = Mathf.Lerp(1.0f, 0.0f, node.weightMap.GetPixel(texX, texY).g); 
                        }

                        void AddSpring(int nx, int ny, float compMult)
                        {
                            if (nx >= 0 && nx < node.resolution.x && ny >= 0 && ny < node.resolution.y)
                            {
                                if (node.isPrayerFlagMode && !(y == 0 && ny == 0) && (x / node.flagWidth) != (nx / node.flagWidth)) return; 
                                int nIdx = ny * node.resolution.x + nx;
                                float dist = Vector3.Distance(positions[index], positions[nIdx]);
                                springs.Add(new Spring { targetIndex = (uint)nIdx, restLength = dist, compliance = compliance * compMult * stiffnessMult, padding = 0 });
                                springCount++;
                            }
                        }

                        AddSpring(x, y - 1, 1f); AddSpring(x, y + 1, 1f); AddSpring(x - 1, y, 1f); AddSpring(x + 1, y, 1f);
                        AddSpring(x - 1, y - 1, 2f); AddSpring(x + 1, y - 1, 2f); AddSpring(x - 1, y + 1, 2f); AddSpring(x + 1, y + 1, 2f);
                        AddSpring(x, y - 2, 4f); AddSpring(x, y + 2, 4f); AddSpring(x - 2, y, 4f); AddSpring(x + 2, y, 4f);

                        springLinks.Add(new SpringLink { startIndex = startIndex, count = springCount });
                    }
                }

                for (int y = 0; y < node.resolution.y - 1; y++)
                {
                    for (int x = 0; x < node.resolution.x - 1; x++)
                    {
                        if (node.isPrayerFlagMode && (x + 1) % node.flagWidth == 0) continue;
                        int i0 = y * node.resolution.x + x;
                        meshIndices.Add(i0); meshIndices.Add(i0 + node.resolution.x); meshIndices.Add(i0 + 1);
                        meshIndices.Add(i0 + 1); meshIndices.Add(i0 + node.resolution.x); meshIndices.Add(i0 + node.resolution.x + 1);
                    }
                }

                // 3. Create the Local Mesh
                RenderMesh = new Mesh { name = "BannerMesh_" + node.name, indexFormat = IndexFormat.UInt32 };
                RenderMesh.SetVertices(new Vector3[VertexCount]); 
                RenderMesh.SetIndices(meshIndices.ToArray(), MeshTopology.Triangles, 0);
                RenderMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f); // Render bounds handles by frustum logic

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

                PositionsBuffer.SetData(positions);
                PredictedPositionsBuffer.SetData(positions);
                NormalsBuffer.SetData(normals);
                UVsBuffer.SetData(uvs);
                PhysicsBuffer.SetData(physics);
                SpringLinksBuffer.SetData(springLinks);
                SpringsBuffer.SetData(springs);
                AdjacencyBuffer.SetData(adjacency);
                
                UpdateDynamicColliders();

                // 5. Initialize the Material Property Block!
                // This tricks the global shader into reading ONLY this instance's buffers for this specific draw call.
                MatBlock = new MaterialPropertyBlock();
                MatBlock.SetBuffer(PositionsBuffer1, PositionsBuffer);
                MatBlock.SetBuffer(Buffer, NormalsBuffer);
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

                    if (c.colliderType == ClothColliderType.Sphere) _colliderDataArray[i].posA = c.transform.position;
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
                PositionsBuffer?.Dispose(); PredictedPositionsBuffer?.Dispose(); NormalsBuffer?.Dispose(); UVsBuffer?.Dispose();
                PhysicsBuffer?.Dispose(); SpringLinksBuffer?.Dispose(); SpringsBuffer?.Dispose(); AdjacencyBuffer?.Dispose();
                HashPairsBuffer?.Dispose(); SortedHashPairsBuffer?.Dispose(); CellOffsetsBuffer?.Dispose(); CollidersBuffer?.Dispose();
            }
        }
    }
}