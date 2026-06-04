using System.Runtime.InteropServices;
using TechArtPlayground.Wind;
using UnityEngine;

namespace TechArtPlayground.Wind.Chimes
{
    [DefaultExecutionOrder(-50)]
    public class ComputeChimeSim : MonoBehaviour
    {
        // ----------------------------------------------------
        // SHADER PROPERTY IDS
        // ----------------------------------------------------
        private static readonly int ChimeCountID = Shader.PropertyToID("_ChimeCount");
        private static readonly int WindVelocityID = Shader.PropertyToID("_WindVelocity");
        private static readonly int WindTurbulenceID = Shader.PropertyToID("_WindTurbulence");
        private static readonly int DeltaTimeID = Shader.PropertyToID("_DeltaTime");
        private static readonly int TimeID = Shader.PropertyToID("_Time");
        private static readonly int GravityID = Shader.PropertyToID("_Gravity");
        private static readonly int DampingID = Shader.PropertyToID("_Damping");
        private static readonly int MaxAngleID = Shader.PropertyToID("_MaxAngle");
        private static readonly int ChimesID = Shader.PropertyToID("Chimes");
        private static readonly int ChimeDataBuffer = Shader.PropertyToID("_ChimeDataBuffer");

        [Header("References")] 
        public ComputeShader chimeCompute;
        public Material instancedMaterial;
        public Mesh chimeMesh;

        // ----------------------------------------------------
        // 1. INSPECTOR & ENCAPSULATED PROPERTIES
        // ----------------------------------------------------
        [Header("Environment Physics")] 
        [SerializeField] private float gravity = 9.81f;
        public float Gravity 
        { 
            get => gravity; 
            set { if (Mathf.Approximately(gravity, value)) return; gravity = value; PushFloat(GravityID, value); } 
        }

        [SerializeField] private float damping = 0.5f;
        public float Damping 
        { 
            get => damping; 
            set { if (Mathf.Approximately(damping, value)) return; damping = value; PushFloat(DampingID, value); } 
        }

        [Header("Constraints")]
        [Tooltip("Angle maximum en degrés avant que le carillon ne soit bloqué.")]
        [Range(10f, 170f)]
        [SerializeField] private float maxSwingAngle = 80f;
        public float MaxSwingAngle 
        { 
            get => maxSwingAngle; 
            set 
            { 
                if (Mathf.Approximately(maxSwingAngle, value)) return; 
                maxSwingAngle = value; 
                PushFloat(MaxAngleID, value * Mathf.Deg2Rad); // Calculate Rads only on change
            } 
        }

        // Hidden dynamic properties driven by weather manager
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
        private GraphicsBuffer _argsBuffer;
        private GraphicsBuffer _chimeDataBuffer;
        
        private int _chimeCount;
        private int _kernelUpdate;
        private int _threadGroupsX; // Cached Dispatch Dimension
        private Bounds _globalBounds;

        private void OnEnable()
        {
            InitializeSystem();
            PushAllComputeData();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            PushAllComputeData();
        }
#endif

        private void PushFloat(int id, float val) { if (chimeCompute != null) chimeCompute.SetFloat(id, val); }
        private void PushVector(int id, Vector3 val) { if (chimeCompute != null) chimeCompute.SetVector(id, val); }

        private void PushAllComputeData()
        {
            PushFloat(GravityID, gravity);
            PushFloat(DampingID, damping);
            PushFloat(MaxAngleID, maxSwingAngle * Mathf.Deg2Rad);
            PushVector(WindVelocityID, _windVelocity);
            PushFloat(WindTurbulenceID, _windTurbulence);
        }

        private void Update()
        {
            if (_chimeCount == 0 || _chimeDataBuffer == null || _argsBuffer == null) return;

            // 1. Wind Polling Firewall
            // Properties intercept redundant assignments inherently via the equality check in the setters.
            if (GlobalWeatherManager.Instance != null)
            {
                WindVelocity = GlobalWeatherManager.Instance.CurrentWindVelocity;
                WindTurbulence = GlobalWeatherManager.Instance.CurrentWindTurbulence;
            }

            // 2. Pure Time Dispatches
            chimeCompute.SetFloat(DeltaTimeID, Time.unscaledDeltaTime);
            chimeCompute.SetFloat(TimeID, Time.unscaledTime);

            // 3. Execution (Using the cached thread group value!)
            chimeCompute.Dispatch(_kernelUpdate, _threadGroupsX, 1, 1);
            Graphics.DrawMeshInstancedIndirect(chimeMesh, 0, instancedMaterial, _globalBounds, _argsBuffer);
        }

        private void InitializeSystem()
        {
            if (chimeCompute == null || instancedMaterial == null || chimeMesh == null) return;

            ChimeNode[] nodes = FindObjectsByType<ChimeNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _chimeCount = nodes.Length;

            if (_chimeCount == 0) return;

            ChimeData[] chimeDataArray = new ChimeData[_chimeCount];
            _globalBounds = new Bounds(nodes[0].transform.position, Vector3.zero);

            for (int i = 0; i < _chimeCount; i++)
            {
                ChimeNode node = nodes[i];
                chimeDataArray[i] = new ChimeData
                {
                    pivotPosition = node.transform.position,
                    mass = node.mass,
                    angle = Vector2.zero,
                    velocity = Vector2.zero,
                    length = node.length,
                    padding = Vector3.zero,
                    transformMatrix = Matrix4x4.identity
                };
                _globalBounds.Encapsulate(node.transform.position);
            }

            _globalBounds.Expand(5.0f);

            // 1. Allocate and Set Data
            int stride = Marshal.SizeOf(typeof(ChimeData));
            _chimeDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _chimeCount, stride);
            _chimeDataBuffer.SetData(chimeDataArray);

            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            args[0] = chimeMesh.GetIndexCount(0);
            args[1] = (uint)_chimeCount;
            args[2] = chimeMesh.GetIndexStart(0);
            args[3] = chimeMesh.GetBaseVertex(0);

            _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, args.Length * sizeof(uint));
            _argsBuffer.SetData(args);

            // 2. Cache Kernels and Execution Dimensions O(1)
            _kernelUpdate = chimeCompute.FindKernel("CSUpdateChimes");
            _threadGroupsX = Mathf.CeilToInt(_chimeCount / 64f);

            // 3. Bind properties that literally never change
            chimeCompute.SetBuffer(_kernelUpdate, ChimesID, _chimeDataBuffer);
            chimeCompute.SetInt(ChimeCountID, _chimeCount);
            instancedMaterial.SetBuffer(ChimeDataBuffer, _chimeDataBuffer);
        }

        private void OnDisable()
        {
            _chimeDataBuffer?.Dispose();
            _argsBuffer?.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ChimeData
        {
            public Vector3 pivotPosition;
            public float mass;
            public Vector2 angle;
            public Vector2 velocity;
            public float length;
            public Vector3 padding;
            public Matrix4x4 transformMatrix;
        }
    }
}