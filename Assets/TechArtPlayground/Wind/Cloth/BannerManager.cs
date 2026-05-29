using System.Collections.Generic;
using System.Runtime.InteropServices;
using TechArtPlayground.Cloth;
using UnityEngine;
using UnityEngine.Rendering;
// UPDATED: Changed from Wind to Weather

namespace TechArtPlayground.Wind.Cloth
{
    public class PhysicsBannerManager : MonoBehaviour
    {
        [Header("Resources")] public ComputeShader clothCompute;

        public Material clothMaterial;

        [Header("Global Physics Settings")] public Vector3 gravity = new(0, -9.81f, 0);

        public float stiffness = 1500f;
        public float damping = 25f;
        public float drag = 1.5f;
        [Range(1, 20)] public int solverIterations = 4;
        private int _kernelPhysics, _kernelResetNormals, _kernelAccumulateNormals, _kernelApplyNormals;
        private Mesh _megaMesh;
        private int _totalVertices, _totalTriangles;

        private GraphicsBuffer _verticesBuffer,
            _physicsBuffer,
            _springLinksBuffer,
            _springsBuffer,
            _trianglesBuffer,
            _normalAccumBuffer;

        private void Start()
        {
            InitializeMegaSimulation();
        }

        private void Update()
        {
            if (_verticesBuffer == null) return;

            float subStepDelta = Time.deltaTime / solverIterations;

            clothCompute.SetFloat("_Time", Time.time);
            clothCompute.SetFloat("_Stiffness", stiffness);
            clothCompute.SetFloat("_Damping", damping);
            clothCompute.SetFloat("_Drag", drag);
            clothCompute.SetVector("_Gravity", gravity);
            clothCompute.SetFloat("_DeltaTime", subStepDelta);

            // =========================================================
            // UPDATED: Read from the new WeatherManager
            // =========================================================
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

            for (int i = 0; i < solverIterations; i++) clothCompute.Dispatch(_kernelPhysics, groupsX_Vertices, 1, 1);

            clothCompute.Dispatch(_kernelResetNormals, groupsX_Vertices, 1, 1);
            clothCompute.Dispatch(_kernelAccumulateNormals, groupsX_Triangles, 1, 1);
            clothCompute.Dispatch(_kernelApplyNormals, groupsX_Vertices, 1, 1);

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
        }

        private void InitializeMegaSimulation()
        {
            PhysicsBannerNode[] nodes =
                FindObjectsByType<PhysicsBannerNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (nodes.Length == 0) return;

            List<VertexData> vertices = new();
            List<PhysicsState> physics = new();
            List<SpringLink> springLinks = new();
            List<Spring> springs = new();
            List<Int3> triangles = new();
            List<int> meshIndices = new();

            int vertexOffset = 0;
            int springOffset = 0;

            foreach (PhysicsBannerNode node in nodes)
            {
                int nodeVertexCount = node.resolution.x * node.resolution.y;
                Vector2 step = new(node.dimensions.x / (node.resolution.x - 1),
                    node.dimensions.y / (node.resolution.y - 1));

                // 1. Sommets & Physique
                for (int y = 0; y < node.resolution.y; y++)
                for (int x = 0; x < node.resolution.x; x++)
                {
                    Vector3 localPos = new(x * step.x - node.dimensions.x * 0.5f, -(y * step.y), 0);
                    Vector3 worldPos = node.transform.TransformPoint(localPos); // Transformation dans le monde !

                    float uCoord = node.isPrayerFlagMode
                        ? (float)(x % node.flagWidth) / (node.flagWidth - 1)
                        : (float)x / (node.resolution.x - 1);

                    vertices.Add(new VertexData
                    {
                        position = worldPos, normal = -node.transform.forward,
                        uv = new Vector2(uCoord, 1.0f - (float)y / (node.resolution.y - 1))
                    });

                    float invMass = 1.0f;
                    if (y == 0)
                    {
                        if (node.isPrayerFlagMode)
                            invMass = x == 0 || x == node.resolution.x - 1 ? 0.0f : 0.5f;
                        else
                            invMass = 0.0f; // Fixé en haut
                    }

                    physics.Add(new PhysicsState { velocity = Vector3.zero, inverseMass = invMass });
                }

                // 2. Ressorts
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

                            float finalStiff =
                                bothRope ? stiffMult * Mathf.Lerp(1.0f, 3.0f, node.ropeTension) : stiffMult;
                            float finalLength = bothRope ? dist * Mathf.Lerp(1.30f, 0.98f, node.ropeTension) : dist;

                            springs.Add(new Spring
                            {
                                targetIndex =
                                    (uint)(neighborIdx + vertexOffset), // IMPORTANT : On ajoute l'offset global !
                                restLength = finalLength, stiffnessMult = finalStiff, padding = 0
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

            _totalVertices = vertices.Count;
            _totalTriangles = triangles.Count;

            // 4. Mega Mesh
            _megaMesh = new Mesh { name = "MegaClothMesh", indexFormat = IndexFormat.UInt32 };
            _megaMesh.SetVertices(new Vector3[_totalVertices]); // Positions bidons, le shader gère le reste
            _megaMesh.SetIndices(meshIndices.ToArray(), MeshTopology.Triangles, 0);
            _megaMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f); // Très grand pour éviter le culling

            // 5. Buffers GPU
            _verticesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 32);
            _physicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 16);
            _springLinksBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices, 8);
            _springsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, springs.Count, 16);
            _trianglesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalTriangles, 12);
            _normalAccumBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _totalVertices * 3, 4);

            _verticesBuffer.SetData(vertices);
            _physicsBuffer.SetData(physics);
            _springLinksBuffer.SetData(springLinks);
            _springsBuffer.SetData(springs);
            _trianglesBuffer.SetData(triangles);

            // 6. Bind Compute
            _kernelPhysics = clothCompute.FindKernel("CSUpdatePhysics");
            _kernelResetNormals = clothCompute.FindKernel("CSResetNormals");
            _kernelAccumulateNormals = clothCompute.FindKernel("CSAccumulateNormals");
            _kernelApplyNormals = clothCompute.FindKernel("CSApplyNormals");

            clothCompute.SetInt("_VertexCount", _totalVertices);
            clothCompute.SetInt("_TriangleCount", _totalTriangles);

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

        // Structures HLSL
        [StructLayout(LayoutKind.Sequential)]
        private struct VertexData
        {
            public Vector3 position;
            public Vector3 normal;
            public Vector2 uv;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PhysicsState
        {
            public Vector3 velocity;
            public float inverseMass;
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
    }
}