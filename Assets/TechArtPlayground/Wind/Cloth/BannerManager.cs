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

        [Header("Global Physics Settings (Hybrid PBD)")]
        public Vector3 gravity = new(0, -9.81f, 0);
        public float stiffness = 5000f; // High stiffness for PBD
        public float damping = 5f;      // Low damping to allow momentum
        public float drag = 0.5f;       // Air drag
        [Range(1, 20)] public int solverIterations = 12;

        [Header("Self-Collision Settings")]
        public float clothThickness = 0.05f; 
        public float gridCellSize = 0.1f;    
        private const int TotalCells = 65536; // Max cells for 16-bit Radix Sort

        // --- Kernels ---
        private int _kernelPhysics, _kernelResetNormals, _kernelAccumulateNormals, _kernelApplyNormals;
        private int _kernelBuildHash, _kernelClearOffsets, _kernelBuildOffsets, _kernelReorderData, _kernelSelfCollision;
        private int _kernelLocalHist, _kernelGlobalScan, _kernelScatter;

        private Mesh _megaMesh;
        private int _totalVertices, _totalTriangles;

        // --- Live Collider Tracking ---
        private ClothStaticCollider[] _activeColliders;
        private ClothColliderData[] _colliderDataArray;

        // --- Core Buffers ---
        private GraphicsBuffer _verticesBuffer,
            _physicsBuffer,
            _springLinksBuffer,
            _springsBuffer,
            _trianglesBuffer,
            _normalAccumBuffer,
            _collidersBuffer;

        // --- Spatial Hashing / Radix Sort Buffers ---
        private GraphicsBuffer _inputHashBuffer,
            _outputHashBuffer,
            _cellOffsetsBuffer,
            _localOffsetsBuffer,
            _globalHistBuffer,
            _sortedPositionsBuffer; // Cache Optimization

        private void Start()
        {
            InitializeMegaSimulation();
        }

        private void Update()
        {
            if (_verticesBuffer == null) return;

            // 1. UPDATE DYNAMIC COLLIDERS
            UpdateDynamicColliders();

            // Prevent Frame 1 physics explosions
            float fixedDelta = 0.016666f; 
            float subStepDelta = fixedDelta / solverIterations;
            clothCompute.SetFloat("_DeltaTime", subStepDelta);

            clothCompute.SetFloat("_Time", Time.time);
            clothCompute.SetFloat("_Stiffness", stiffness);
            clothCompute.SetFloat("_Damping", damping);
            clothCompute.SetFloat("_Drag", drag);
            clothCompute.SetVector("_Gravity", gravity);

            // 2. WEATHER / WIND
            Vector3 currentWindVel = WeatherManager.Instance != null
                ? WeatherManager.Instance.CurrentWindVelocity
                : Vector3.zero;

            float currentWindTurb = WeatherManager.Instance != null
                ? WeatherManager.Instance.windGusts
                : 0f;

            clothCompute.SetVector("_WindVelocity", currentWindVel);
            clothCompute.SetFloat("_WindTurbulence", currentWindTurb);

            int groupsX_Vertices = Mathf.CeilToInt(_totalVertices / 64f);
            int groupsX_Triangles = Mathf.CeilToInt(_totalTriangles / 64f);

            // 3. PBD SUB-STEPPING LOOP
            for (int i = 0; i < solverIterations; i++)
            {
                // A. Integrate Physics & Static Collisions
                clothCompute.Dispatch(_kernelPhysics, groupsX_Vertices, 1, 1);

                // B. Resolve Self-Collisions via Radix Spatial Hash
                DispatchSelfCollision(groupsX_Vertices);
            }

            // 4. CALCULATE NORMALS
            clothCompute.Dispatch(_kernelResetNormals, groupsX_Vertices, 1, 1);
            clothCompute.Dispatch(_kernelAccumulateNormals, groupsX_Triangles, 1, 1);
            clothCompute.Dispatch(_kernelApplyNormals, groupsX_Vertices, 1, 1);

            // 5. DRAW MESH
            Graphics.DrawMesh(_megaMesh, Matrix4x4.identity, clothMaterial, gameObject.layer);
        }

        private void OnDestroy()
        {
            _verticesBuffer?.Dispose();
            _physicsBuffer?.Dispose();
            _springLinksBuffer?.Dispose();
            _springsBuffer?.Dispose();
            _trianglesBuffer?.Dispose();
            _normalAccumBuffer?.Dispose();
            _collidersBuffer?.Dispose();

            _inputHashBuffer?.Dispose();
            _outputHashBuffer?.Dispose();
            _cellOffsetsBuffer?.Dispose();
            _localOffsetsBuffer?.Dispose();
            _globalHistBuffer?.Dispose();
            _sortedPositionsBuffer?.Dispose();
        }

        private void DispatchSelfCollision(int groupsX_Vertices)
        {
            clothCompute.SetFloat("_GridCellSize", gridCellSize);
            clothCompute.SetFloat("_ClothThickness", clothThickness);
            clothCompute.SetInt("_TotalCells", TotalCells);

            // 1. Build Initial Hashes
            clothCompute.SetBuffer(_kernelBuildHash, "Vertices", _verticesBuffer);
            clothCompute.SetBuffer(_kernelBuildHash, "OutputBuffer", _outputHashBuffer);
            clothCompute.Dispatch(_kernelBuildHash, groupsX_Vertices, 1, 1);

            // 2. RADIX SORT (4 Passes)
            int numBlocks = Mathf.CeilToInt((float)_totalVertices / 256f);
            clothCompute.SetInt("numElements", _totalVertices);
            clothCompute.SetInt("numBlocks", numBlocks);

            for (int pass = 0; pass < 4; pass++)
            {
                clothCompute.SetInt("bitShift", pass * 4);

                GraphicsBuffer inBuf = (pass % 2 == 0) ? _outputHashBuffer : _inputHashBuffer;
                GraphicsBuffer outBuf = (pass % 2 == 0) ? _inputHashBuffer : _outputHashBuffer;

                clothCompute.SetBuffer(_kernelLocalHist, "InputBuffer", inBuf);
                clothCompute.SetBuffer(_kernelLocalHist, "LocalOffsets", _localOffsetsBuffer);
                clothCompute.SetBuffer(_kernelLocalHist, "GlobalHist", _globalHistBuffer);
                clothCompute.Dispatch(_kernelLocalHist, numBlocks, 1, 1);

                clothCompute.SetBuffer(_kernelGlobalScan, "GlobalHist", _globalHistBuffer);
                clothCompute.Dispatch(_kernelGlobalScan, 16, 1, 1);

                clothCompute.SetBuffer(_kernelScatter, "InputBuffer", inBuf);
                clothCompute.SetBuffer(_kernelScatter, "OutputBuffer", outBuf);
                clothCompute.SetBuffer(_kernelScatter, "LocalOffsets", _localOffsetsBuffer);
                clothCompute.SetBuffer(_kernelScatter, "GlobalHist", _globalHistBuffer);
                clothCompute.Dispatch(_kernelScatter, numBlocks, 1, 1);
            }

            // 3. Clear and Build Cell Offsets
            int groupsX_Cells = Mathf.CeilToInt(TotalCells / 256f);
            clothCompute.SetBuffer(_kernelClearOffsets, "CellOffsets", _cellOffsetsBuffer);
            clothCompute.Dispatch(_kernelClearOffsets, groupsX_Cells, 1, 1);

            clothCompute.SetBuffer(_kernelBuildOffsets, "OutputBuffer", _outputHashBuffer);
            clothCompute.SetBuffer(_kernelBuildOffsets, "CellOffsets", _cellOffsetsBuffer);
            clothCompute.Dispatch(_kernelBuildOffsets, groupsX_Vertices, 1, 1);

            // 4. CACHE OPTIMIZATION: Reorder Positions Array
            clothCompute.SetBuffer(_kernelReorderData, "Vertices", _verticesBuffer);
            clothCompute.SetBuffer(_kernelReorderData, "OutputBuffer", _outputHashBuffer);
            clothCompute.SetBuffer(_kernelReorderData, "SortedPositions", _sortedPositionsBuffer);
            clothCompute.Dispatch(_kernelReorderData, groupsX_Vertices, 1, 1);

            // 5. Resolve Self Collision (PBD Push)
            clothCompute.SetBuffer(_kernelSelfCollision, "Vertices", _verticesBuffer);
            clothCompute.SetBuffer(_kernelSelfCollision, "PhysicsData", _physicsBuffer);
            clothCompute.SetBuffer(_kernelSelfCollision, "OutputBuffer", _outputHashBuffer);
            clothCompute.SetBuffer(_kernelSelfCollision, "CellOffsets", _cellOffsetsBuffer);
            clothCompute.SetBuffer(_kernelSelfCollision, "SortedPositions", _sortedPositionsBuffer);
            clothCompute.Dispatch(_kernelSelfCollision, groupsX_Vertices, 1, 1);
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

            _collidersBuffer.SetData(_colliderDataArray);
        }

        private void InitializeMegaSimulation()
        {
            PhysicsBannerNode[] nodes = FindObjectsByType<PhysicsBannerNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (nodes.Length == 0) return;

            List<VertexData> vertices = new();
            List<PhysicsState> physics = new();
            List<SpringLink> springLinks = new();
            List<Spring> springs = new();
            List<Int3> triangles = new();
            List<int> meshIndices = new();

            List<ClothStaticCollider> safeColliderTracker = new();
            List<ClothColliderData> globalCollidersData = new();

            int vertexOffset = 0;

            foreach (PhysicsBannerNode node in nodes)
            {
                // --- PROCESS COLLIDERS FOR THIS NODE ---
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

                // 1. Vertices & Physics
                for (int y = 0; y < node.resolution.y; y++)
                    for (int x = 0; x < node.resolution.x; x++)
                    {
                        Vector3 localPos = new(x * step.x - node.dimensions.x * 0.5f, -(y * step.y), 0);
                        Vector3 worldPos = node.transform.TransformPoint(localPos);

                        float uCoord = node.isPrayerFlagMode
                            ? (float)(x % node.flagWidth) / (node.flagWidth - 1)
                            : (float)x / (node.resolution.x - 1);
                        
                        float vCoord = 1.0f - (float)y / (node.resolution.y - 1);

                        vertices.Add(new VertexData
                        {
                            position = worldPos,
                            normal = -node.transform.forward,
                            uv = new Vector2(uCoord, vCoord)
                        });

                        // --- PAINTED MASK LOGIC ---
                        float invMass = 1.0f;
                        float selfCollide = 1.0f; 

                        // Note: To use node.weightMap, ensure the field exists in PhysicsBannerNode.cs
                        // and the Texture Import Settings have "Read/Write" enabled.
                        // Assuming you added `public Texture2D weightMap;` to PhysicsBannerNode:
                        
                        /* UNCOMMENT IF WEIGHT MAP FIELD IS ADDED TO BANNER NODE:
                        if (node.weightMap != null)
                        {
                            int texX = Mathf.Clamp(Mathf.RoundToInt(uCoord * (node.weightMap.width - 1)), 0, node.weightMap.width - 1);
                            int texY = Mathf.Clamp(Mathf.RoundToInt(vCoord * (node.weightMap.height - 1)), 0, node.weightMap.height - 1);
                            Color paintColor = node.weightMap.GetPixel(texX, texY);

                            invMass = paintColor.r; 
                            selfCollide = paintColor.g; 
                        }
                        else */
                        {
                            // Fallback Logic
                            if (y == 0)
                            {
                                if (node.isPrayerFlagMode) invMass = x == 0 || x == node.resolution.x - 1 ? 0.0f : 0.5f;
                                else invMass = 0.0f; // Pinned
                            }
                        }

                        physics.Add(new PhysicsState
                        {
                            velocity = Vector3.zero,
                            inverseMass = invMass,
                            colliderStart = colliderStart,
                            colliderCount = colliderCount,
                            selfCollideMask = selfCollide,
                            padding = 0f
                        });
                    }

                // 2. Springs
                for (int y = 0; y < node.resolution.y; y++)
                    for (int x = 0; x < node.resolution.x; x++)
                    {
                        int index = y * node.resolution.x + x;
                        uint startIndex = (uint)springs.Count;
                        uint springCount = 0;

                        void AddSpring(int nx, int ny, float stiffMult)
                        {
                            if (nx >= 0 && nx < node.resolution.x && ny >= 0 && ny < node.resolution.y)
                            {
                                bool bothRope = node.isPrayerFlagMode && y == 0 && ny == 0;
                                if (node.isPrayerFlagMode && !bothRope)
                                {
                                    int minX = Mathf.Min(x, nx);
                                    if (nx != x && (minX + 1) % node.flagWidth == 0) return;
                                }

                                int neighborIdx = ny * node.resolution.x + nx;
                                float dist = Vector3.Distance(vertices[vertexOffset + index].position,
                                    vertices[vertexOffset + neighborIdx].position);

                                float finalStiff = bothRope ? stiffMult * Mathf.Lerp(1.0f, 3.0f, node.ropeTension) : stiffMult;
                                float finalLength = bothRope ? dist * Mathf.Lerp(1.30f, 0.98f, node.ropeTension) : dist;

                                springs.Add(new Spring
                                {
                                    targetIndex = (uint)(neighborIdx + vertexOffset),
                                    restLength = finalLength,
                                    stiffnessMult = finalStiff,
                                    padding = 0
                                });
                                springCount++;
                            }
                        }

                        AddSpring(x, y - 1, 1.0f);
                        AddSpring(x, y + 1, 1.0f);
                        AddSpring(x - 1, y, 1.0f);
                        AddSpring(x + 1, y, 1.0f);
                        AddSpring(x - 1, y - 1, 0.75f);
                        AddSpring(x + 1, y - 1, 0.75f);
                        AddSpring(x - 1, y + 1, 0.75f);
                        AddSpring(x + 1, y + 1, 0.75f);

                        springLinks.Add(new SpringLink { startIndex = startIndex, count = springCount });
                    }

                // 3. Triangles
                for (int y = 0; y < node.resolution.y - 1; y++)
                    for (int x = 0; x < node.resolution.x - 1; x++)
                    {
                        if (node.isPrayerFlagMode && (x + 1) % node.flagWidth == 0) continue;

                        int i0 = vertexOffset + y * node.resolution.x + x;
                        int i1 = i0 + 1;
                        int i2 = i0 + node.resolution.x;
                        int i3 = i2 + 1;

                        triangles.Add(new Int3 { x = i0, y = i2, z = i1 });
                        meshIndices.Add(i0);
                        meshIndices.Add(i2);
                        meshIndices.Add(i1);

                        triangles.Add(new Int3 { x = i1, y = i2, z = i3 });
                        meshIndices.Add(i1);
                        meshIndices.Add(i2);
                        meshIndices.Add(i3);
                    }

                vertexOffset += nodeVertexCount;
            }

            _activeColliders = safeColliderTracker.ToArray();
            _colliderDataArray = globalCollidersData.ToArray();

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

            _totalVertices = vertices.Count;
            _totalTriangles = triangles.Count;

            _megaMesh = new Mesh { name = "MegaClothMesh", indexFormat = IndexFormat.UInt32 };
            _megaMesh.SetVertices(new Vector3[_totalVertices]);
            _megaMesh.SetIndices(meshIndices.ToArray(), MeshTopology.Triangles, 0);
            _megaMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

            // --- ALLOCATE GPU BUFFERS ---
            
            // Core
            _verticesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 32);
            _physicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 32);
            _springLinksBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 8);
            _springsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, springs.Count, 16);
            _trianglesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalTriangles, 12);
            _normalAccumBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices * 3, 4);

            // Radix Sort & Cache SoA
            int numBlocks = Mathf.CeilToInt((float)_totalVertices / 256f);
            _inputHashBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 8); 
            _outputHashBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 8);
            _cellOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, TotalCells, 8);   
            _localOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 4); 
            _globalHistBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 16 * numBlocks, 4);   
            _sortedPositionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 12); // float3

            _verticesBuffer.SetData(vertices);
            _physicsBuffer.SetData(physics);
            _springLinksBuffer.SetData(springLinks);
            _springsBuffer.SetData(springs);
            _trianglesBuffer.SetData(triangles);

            // --- FIND KERNELS ---
            _kernelPhysics = clothCompute.FindKernel("CSUpdatePhysics");
            _kernelResetNormals = clothCompute.FindKernel("CSResetNormals");
            _kernelAccumulateNormals = clothCompute.FindKernel("CSAccumulateNormals");
            _kernelApplyNormals = clothCompute.FindKernel("CSApplyNormals");

            _kernelBuildHash = clothCompute.FindKernel("CSBuildHash");
            _kernelClearOffsets = clothCompute.FindKernel("CSClearOffsets");
            _kernelBuildOffsets = clothCompute.FindKernel("CSBuildOffsets");
            _kernelReorderData = clothCompute.FindKernel("CSReorderData");
            _kernelSelfCollision = clothCompute.FindKernel("CSSelfCollision");

            _kernelLocalHist = clothCompute.FindKernel("LocalHistogram");
            _kernelGlobalScan = clothCompute.FindKernel("GlobalScan");
            _kernelScatter = clothCompute.FindKernel("Scatter");

            clothCompute.SetInt("_VertexCount", _totalVertices);
            clothCompute.SetInt("_TriangleCount", _totalTriangles);

            // --- BIND PERSISTENT BUFFERS ---
            clothCompute.SetBuffer(_kernelPhysics, "Vertices", _verticesBuffer);
            clothCompute.SetBuffer(_kernelPhysics, "PhysicsData", _physicsBuffer);
            clothCompute.SetBuffer(_kernelPhysics, "SpringLinks", _springLinksBuffer);
            clothCompute.SetBuffer(_kernelPhysics, "Springs", _springsBuffer);
            clothCompute.SetBuffer(_kernelPhysics, "Colliders", _collidersBuffer);

            clothCompute.SetBuffer(_kernelResetNormals, "NormalAccumBuffer", _normalAccumBuffer);
            clothCompute.SetBuffer(_kernelAccumulateNormals, "Triangles", _trianglesBuffer);
            clothCompute.SetBuffer(_kernelAccumulateNormals, "Vertices", _verticesBuffer);
            clothCompute.SetBuffer(_kernelAccumulateNormals, "NormalAccumBuffer", _normalAccumBuffer);
            clothCompute.SetBuffer(_kernelApplyNormals, "Vertices", _verticesBuffer);
            clothCompute.SetBuffer(_kernelApplyNormals, "NormalAccumBuffer", _normalAccumBuffer);

            clothMaterial.SetBuffer("_VertexDataBuffer", _verticesBuffer);
        }

        // --- C# Equivalents of HLSL Structs ---
        [StructLayout(LayoutKind.Sequential)]
        private struct VertexData
        {
            public Vector3 position;
            public Vector3 normal;
            public Vector2 uv;
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
            public float stiffnessMult;
            public float padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Int3
        {
            public int x, y, z;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct PhysicsState
        {
            public Vector3 velocity;
            public float inverseMass;
            public uint colliderStart;
            public uint colliderCount;
            public float selfCollideMask; // GPU Mask variable
            public float padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VertexHashPair
        {
            public uint vertexIndex;
            public uint cellHash;
        }
    }
}