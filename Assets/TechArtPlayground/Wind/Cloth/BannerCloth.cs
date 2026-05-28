using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TechArtPlayground.Cloth
{
    public class ComputeClothSim : MonoBehaviour
    {
        [Header("Resources")]
        public ComputeShader clothCompute;
        public Material clothMaterial;

        [Header("Cloth Settings")]
        public Vector2Int resolution = new Vector2Int(32, 32);
        public Vector2 dimensions = new Vector2(5f, 5f);
    
        [Header("Prayer Flag Mode")]
        [Tooltip("If true, slices the cloth into vertical ribbons attached to a main top rope.")]
        public bool isPrayerFlagMode = false;
        [Tooltip("How many vertices wide each flag should be.")]
        public int flagWidth = 5;
        [Tooltip("1.0 is a tight straight line. 0.0 is a loose, deep sagging curve.")]
        [Range(0.0f, 10.0f)] public float ropeTension = 0.5f;
    
        [Header("Physics Parameters")]
        public Vector3 gravity = new Vector3(0, -9.81f, 0);
        public Vector3 windVelocity = new Vector3(2f, 0, 5f);
        [Range(0f, 2f)] public float windTurbulence = 0.5f;
        public float stiffness = 1500f;
        public float damping = 25f;
        public float drag = 1.5f;
    
        [Header("Solver Settings")]
        [Tooltip("Resolver Frequency: Higher values increase stability and stiffness but cost more performance.")]
        [Range(1, 20)] public int solverIterations = 4;

        // --- C# Equivalents of HLSL Structs ---
        [StructLayout(LayoutKind.Sequential)]
        struct VertexData {
            public Vector3 position;
            public Vector3 normal;
            public Vector2 uv;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PhysicsState {
            public Vector3 velocity;
            public float inverseMass;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SpringLink {
            public uint startIndex;
            public uint count;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Spring {
            public uint targetIndex;
            public float restLength;
            public float stiffnessMult;
            public float padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Int3 {
            public int x, y, z;
        }

        // --- Buffers ---
        private GraphicsBuffer _verticesBuffer;
        private GraphicsBuffer _physicsBuffer;
        private GraphicsBuffer _springLinksBuffer;
        private GraphicsBuffer _springsBuffer;
        private GraphicsBuffer _trianglesBuffer;
        private GraphicsBuffer _normalAccumBuffer;

        private Mesh _dummyMesh;
        private int _vertexCount;
        private int _triangleCount;

        // Kernel IDs
        private int _kernelPhysics, _kernelResetNormals, _kernelAccumulateNormals, _kernelApplyNormals;

        // Live Update Tracker
        private float _lastRopeTension = -1f;

        void Start()
        {
            _lastRopeTension = ropeTension;
            InitializeSimulation();
        }

        private void InitializeSimulation()
        {
            _vertexCount = resolution.x * resolution.y;
        
            VertexData[] vertices = new VertexData[_vertexCount];
            PhysicsState[] physics = new PhysicsState[_vertexCount];
        
            List<SpringLink> springLinks = new List<SpringLink>(_vertexCount);
            List<Spring> springs = new List<Spring>();
        
            Vector2 step = new Vector2(dimensions.x / (resolution.x - 1), dimensions.y / (resolution.y - 1));

            // 1. Generate Vertices & Physics State
            for (int y = 0; y < resolution.y; y++)
            {
                for (int x = 0; x < resolution.x; x++)
                {
                    int index = y * resolution.x + x;
                
                    Vector3 pos = transform.position + new Vector3(
                        (x * step.x) - (dimensions.x * 0.5f), 
                        dimensions.y - (y * step.y), 
                        0);

                    float uCoord = isPrayerFlagMode ? (float)(x % flagWidth) / (flagWidth - 1) : (float)x / (resolution.x - 1);
                
                    vertices[index] = new VertexData {
                        position = pos,
                        normal = Vector3.back,
                        uv = new Vector2(uCoord, (float)y / (resolution.y - 1))
                    };

                    float invMass = 1.0f;
                    if (y == 0)
                    {
                        if (isPrayerFlagMode) {
                            if (x == 0 || x == resolution.x - 1) invMass = 0.0f;
                            else invMass = 0.5f; 
                        } else {
                            invMass = 0.0f;
                        }
                    }
                
                    physics[index] = new PhysicsState { velocity = Vector3.zero, inverseMass = invMass };
                }
            }

            // 2. Generate Springs
            for (int y = 0; y < resolution.y; y++)
            {
                for (int x = 0; x < resolution.x; x++)
                {
                    int index = y * resolution.x + x;
                    uint startIndex = (uint)springs.Count;
                    uint springCount = 0;

                    void AddSpring(int nx, int ny, float stiffMult)
                    {
                        if (nx >= 0 && nx < resolution.x && ny >= 0 && ny < resolution.y)
                        {
                            bool bothRope = false;

                            if (isPrayerFlagMode)
                            {
                                bothRope = (y == 0 && ny == 0);
                                if (!bothRope)
                                {
                                    int minX = Mathf.Min(x, nx);
                                    if (nx != x && (minX + 1) % flagWidth == 0) return; 
                                }
                            }

                            int neighborIdx = ny * resolution.x + nx;
                            float dist = Vector3.Distance(vertices[index].position, vertices[neighborIdx].position);
                            
                            float finalStiffnessMult = stiffMult;
                            float finalRestLength = dist;

                            if (bothRope)
                            {
                                finalStiffnessMult *= Mathf.Lerp(1.0f, 3.0f, ropeTension);
                                float slackMultiplier = Mathf.Lerp(1.30f, 0.98f, ropeTension);
                                finalRestLength *= slackMultiplier;
                            }

                            springs.Add(new Spring { 
                                targetIndex = (uint)neighborIdx, 
                                restLength = finalRestLength, 
                                stiffnessMult = finalStiffnessMult, 
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
            }

            // 3. Generate Triangles
            int numQuads = (resolution.x - 1) * (resolution.y - 1);
            _triangleCount = numQuads * 2;
        
            List<Int3> triangles = new List<Int3>(_triangleCount);
            List<int> meshIndices = new List<int>(_triangleCount * 3);
        
            for (int y = 0; y < resolution.y - 1; y++)
            {
                for (int x = 0; x < resolution.x - 1; x++)
                {
                    if (isPrayerFlagMode && ((x + 1) % flagWidth == 0)) continue;

                    int i0 = y * resolution.x + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + resolution.x;
                    int i3 = i2 + 1;

                    triangles.Add(new Int3 { x = i0, y = i2, z = i1 });
                    meshIndices.Add(i0); meshIndices.Add(i2); meshIndices.Add(i1);
                
                    triangles.Add(new Int3 { x = i1, y = i2, z = i3 });
                    meshIndices.Add(i1); meshIndices.Add(i2); meshIndices.Add(i3);
                }
            }
        
            _triangleCount = triangles.Count;

            // 4. Create Dummy Mesh
            _dummyMesh = new Mesh { name = "ComputeClothMesh" };
            _dummyMesh.SetVertices(new Vector3[_vertexCount]);
            _dummyMesh.SetIndices(meshIndices.ToArray(), MeshTopology.Triangles, 0);
            _dummyMesh.bounds = new Bounds(transform.position, new Vector3(dimensions.x * 5, dimensions.y * 5, dimensions.x * 5));

            // 5. Allocate Buffers
            _verticesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _vertexCount, 32);
            _physicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _vertexCount, 16);
            _springLinksBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _vertexCount, 8);
            _springsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, springs.Count, 16);
            _trianglesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _triangleCount, 12);
            _normalAccumBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _vertexCount * 3, 4);

            // 6. Push Data
            _verticesBuffer.SetData(vertices);
            _physicsBuffer.SetData(physics);
            _springLinksBuffer.SetData(springLinks);
            _springsBuffer.SetData(springs.ToArray());
            _trianglesBuffer.SetData(triangles);

            // 7. Setup Compute
            _kernelPhysics = clothCompute.FindKernel("CSUpdatePhysics");
            _kernelResetNormals = clothCompute.FindKernel("CSResetNormals");
            _kernelAccumulateNormals = clothCompute.FindKernel("CSAccumulateNormals");
            _kernelApplyNormals = clothCompute.FindKernel("CSApplyNormals");

            clothCompute.SetInt("_VertexCount", _vertexCount);
            clothCompute.SetInt("_TriangleCount", _triangleCount);
        
            clothCompute.SetBuffer(_kernelPhysics, "Vertices", _verticesBuffer);
            clothCompute.SetBuffer(_kernelPhysics, "PhysicsData", _physicsBuffer);
            clothCompute.SetBuffer(_kernelPhysics, "SpringLinks", _springLinksBuffer);
            clothCompute.SetBuffer(_kernelPhysics, "Springs", _springsBuffer);

            clothCompute.SetBuffer(_kernelResetNormals, "NormalAccumBuffer", _normalAccumBuffer);
        
            clothCompute.SetBuffer(_kernelAccumulateNormals, "Triangles", _trianglesBuffer);
            clothCompute.SetBuffer(_kernelAccumulateNormals, "Vertices", _verticesBuffer);
            clothCompute.SetBuffer(_kernelAccumulateNormals, "NormalAccumBuffer", _normalAccumBuffer);
        
            clothCompute.SetBuffer(_kernelApplyNormals, "Vertices", _verticesBuffer);
            clothCompute.SetBuffer(_kernelApplyNormals, "NormalAccumBuffer", _normalAccumBuffer);

            clothMaterial.SetBuffer("_VertexDataBuffer", _verticesBuffer);
        }

        void Update()
        {
            if (_verticesBuffer == null) return;

            // --- THE LIVE TENSION HOOK ---
            if (Mathf.Abs(ropeTension - _lastRopeTension) > 0.001f)
            {
                UpdateTensionLive();
                _lastRopeTension = ropeTension;
            }

            solverIterations = Mathf.Max(1, solverIterations); 
            float subStepDelta = Time.deltaTime / solverIterations;

            clothCompute.SetFloat("_Time", Time.time);
            clothCompute.SetFloat("_Stiffness", stiffness);
            clothCompute.SetFloat("_Damping", damping);
            clothCompute.SetFloat("_Drag", drag);
            clothCompute.SetVector("_Gravity", gravity);
            clothCompute.SetVector("_WindVelocity", windVelocity);
            clothCompute.SetFloat("_WindTurbulence", windTurbulence);
            clothCompute.SetFloat("_DeltaTime", subStepDelta);

            int groupsX_Vertices = Mathf.CeilToInt(_vertexCount / 64f);
            int groupsX_Triangles = Mathf.CeilToInt(_triangleCount / 64f);

            for (int i = 0; i < solverIterations; i++)
            {
                clothCompute.Dispatch(_kernelPhysics, groupsX_Vertices, 1, 1);
            }
        
            clothCompute.Dispatch(_kernelResetNormals, groupsX_Vertices, 1, 1);
            clothCompute.Dispatch(_kernelAccumulateNormals, groupsX_Triangles, 1, 1);
            clothCompute.Dispatch(_kernelApplyNormals, groupsX_Vertices, 1, 1);

            Graphics.DrawMesh(_dummyMesh, Matrix4x4.identity, clothMaterial, gameObject.layer);
        }

        // --- NEW METHOD: Pushes updated lengths to the GPU immediately ---
        private void UpdateTensionLive()
        {
            if (_springsBuffer == null) return;

            List<Spring> springs = new List<Spring>();
            Vector2 step = new Vector2(dimensions.x / (resolution.x - 1), dimensions.y / (resolution.y - 1));

            for (int y = 0; y < resolution.y; y++)
            {
                for (int x = 0; x < resolution.x; x++)
                {
                    void AddSpring(int nx, int ny, float stiffMult)
                    {
                        if (nx >= 0 && nx < resolution.x && ny >= 0 && ny < resolution.y)
                        {
                            bool bothRope = false;

                            if (isPrayerFlagMode)
                            {
                                bothRope = (y == 0 && ny == 0);
                                if (!bothRope)
                                {
                                    int minX = Mathf.Min(x, nx);
                                    if (nx != x && (minX + 1) % flagWidth == 0) return; 
                                }
                            }

                            int neighborIdx = ny * resolution.x + nx;
                            
                            // 1. Calculate the ideal bind-pose mathematically
                            float dx = (nx - x) * step.x;
                            float dy = (ny - y) * step.y;
                            float dist = Mathf.Sqrt(dx * dx + dy * dy);
                            
                            float finalStiffnessMult = stiffMult;
                            float finalRestLength = dist;

                            // 2. Apply Slack
                            if (bothRope)
                            {
                                finalStiffnessMult *= Mathf.Lerp(1.0f, 3.0f, ropeTension);
                                float slackMultiplier = Mathf.Lerp(1.30f, 0.98f, ropeTension);
                                finalRestLength *= slackMultiplier;
                            }

                            springs.Add(new Spring { 
                                targetIndex = (uint)neighborIdx, 
                                restLength = finalRestLength, 
                                stiffnessMult = finalStiffnessMult, 
                                padding = 0 
                            });
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
                }
            }
            
            // 3. Immediately overwrite the GPU buffer with the new tension
            _springsBuffer.SetData(springs.ToArray());
        }

        void OnDestroy()
        {
            _verticesBuffer?.Dispose();
            _physicsBuffer?.Dispose();
            _springLinksBuffer?.Dispose();
            _springsBuffer?.Dispose();
            _trianglesBuffer?.Dispose();
            _normalAccumBuffer?.Dispose();
        }
    }
}