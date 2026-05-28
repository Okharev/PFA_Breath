using System.Runtime.InteropServices;
using UnityEngine;

namespace TechArtPlayground.Wind.Chimes
{
    [DefaultExecutionOrder(-50)]
    public class ComputeChimeSim : MonoBehaviour
    {
        [Header("References")]
        public ComputeShader chimeCompute;
        public Material instancedMaterial;
        public Mesh chimeMesh;

        [Header("Environment Physics")]
        public Vector3 windVelocity = new Vector3(5f, 0f, 2f);
        [Range(0f, 5f)] public float windTurbulence = 1.5f;
        public float gravity = 9.81f; 
        public float damping = 0.5f;

        private GraphicsBuffer _chimeDataBuffer;
        private GraphicsBuffer _argsBuffer;
        private int _chimeCount;
        private Bounds _globalBounds;
        private int _kernelUpdate; 
        
        [StructLayout(LayoutKind.Sequential)]
        struct ChimeData {
            public Vector3 pivotPosition; // 12 bytes
            public float mass;            // 4 bytes  (Block 1: 16 bytes)
            
            public Vector2 angle;         // 8 bytes
            public Vector2 velocity;      // 8 bytes  (Block 2: 16 bytes)
            
            public float length;          // 4 bytes
            public Vector3 padding;       // 12 bytes (Block 3: 16 bytes)
            
            public Matrix4x4 transformMatrix; // 64 bytes (Block 4: 64 bytes)
        }

        void Start()
        {
            InitializeSystem();
        }

        private void InitializeSystem()
        {
            // --- 1. STRICT VALIDATION CHECKS ---
            if (chimeCompute == null) { Debug.LogError("Compute Shader is missing!"); return; }
            if (instancedMaterial == null) { Debug.LogError("Instanced Material is missing!"); return; }
            if (chimeMesh == null) { Debug.LogError("Chime Mesh is missing! Assign a Cylinder in the Inspector."); return; }

            ChimeNode[] nodes = Object.FindObjectsByType<ChimeNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _chimeCount = nodes.Length;

            if (_chimeCount == 0)
            {
                Debug.LogWarning("No ChimeNodes found in the scene! Place some Chime Prefabs.");
                return;
            }

            ChimeData[] chimeDataArray = new ChimeData[_chimeCount];
            _globalBounds = new Bounds(nodes[0].transform.position, Vector3.zero);

            for (int i = 0; i < _chimeCount; i++)
            {
                ChimeNode node = nodes[i];
                
                chimeDataArray[i] = new ChimeData {
                    pivotPosition = node.transform.position,
                    mass = node.mass,
                    angle = Vector2.zero,
                    velocity = Vector2.zero,
                    length = node.length,
                    padding = Vector3.zero, // Explicitly feed zeroes to the padding
                    transformMatrix = Matrix4x4.identity
                };

                _globalBounds.Encapsulate(node.transform.position);
            }

            _globalBounds.Expand(5.0f); 

            // Setup Data Buffer
// Setup Data Buffer dynamically calculating the exact byte size
            int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(ChimeData));
            _chimeDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _chimeCount, stride);

            // Setup Args Buffer
            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            args[0] = (uint)chimeMesh.GetIndexCount(0); 
            args[1] = (uint)_chimeCount;                
            args[2] = (uint)chimeMesh.GetIndexStart(0); 
            args[3] = (uint)chimeMesh.GetBaseVertex(0); 
            
            _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, args.Length * sizeof(uint));
            _argsBuffer.SetData(args);

            _kernelUpdate = chimeCompute.FindKernel("CSUpdateChimes");
            chimeCompute.SetBuffer(_kernelUpdate, "Chimes", _chimeDataBuffer);
            chimeCompute.SetInt("_ChimeCount", _chimeCount);
            
            instancedMaterial.SetBuffer("_ChimeDataBuffer", _chimeDataBuffer);
        }

        void Update()
        {
            // --- 2. UPDATED NULL CHECKS ---
            if (_chimeCount == 0 || _chimeDataBuffer == null || _argsBuffer == null) return;

            chimeCompute.SetFloat("_DeltaTime", Time.deltaTime);
            chimeCompute.SetFloat("_Time", Time.time);
            chimeCompute.SetVector("_WindVelocity", windVelocity);
            chimeCompute.SetFloat("_WindTurbulence", windTurbulence);
            chimeCompute.SetFloat("_Gravity", gravity);
            chimeCompute.SetFloat("_Damping", damping);

            int threadGroupsX = Mathf.CeilToInt(_chimeCount / 64f);
            chimeCompute.Dispatch(_kernelUpdate, threadGroupsX, 1, 1);

            Graphics.DrawMeshInstancedIndirect(chimeMesh, 0, instancedMaterial, _globalBounds, _argsBuffer);
        }

        void OnDestroy()
        {
            _chimeDataBuffer?.Dispose();
            _argsBuffer?.Dispose();
        }
    }
}