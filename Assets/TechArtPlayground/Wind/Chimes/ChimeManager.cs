using System.Runtime.InteropServices;
using TechArtPlayground.Wind;
using UnityEngine;
using R3;

namespace TechArtPlayground.Wind.Chimes
{
    [DefaultExecutionOrder(-50)]
    public class ComputeChimeSim : MonoBehaviour
    {
        // ----------------------------------------------------
        // SHADER PROPERTY IDS
        // ----------------------------------------------------
        private static readonly int ChimeCount = Shader.PropertyToID("_ChimeCount");
        private static readonly int WindVelocity = Shader.PropertyToID("_WindVelocity");
        private static readonly int WindTurbulence = Shader.PropertyToID("_WindTurbulence");
        private static readonly int DeltaTime = Shader.PropertyToID("_DeltaTime");
        private static readonly int Time1 = Shader.PropertyToID("_Time");
        private static readonly int Gravity = Shader.PropertyToID("_Gravity");
        private static readonly int Damping = Shader.PropertyToID("_Damping");
        private static readonly int MaxAngle = Shader.PropertyToID("_MaxAngle");
        private static readonly int Chimes1 = Shader.PropertyToID("Chimes");

        [Header("References")] 
        public ComputeShader chimeCompute;
        public Material instancedMaterial;
        public Mesh chimeMesh;

        // ----------------------------------------------------
        // 1. INSPECTOR FRONT-END
        // ----------------------------------------------------
        [Header("Environment Physics")] 
        [SerializeField] private float gravity = 9.81f;
        [SerializeField] private float damping = 0.5f;

        [Header("Constraints")]
        [Tooltip("Angle maximum en degrés avant que le carillon ne soit bloqué.")]
        [Range(10f, 170f)]
        [SerializeField] private float maxSwingAngle = 80f;

        // ----------------------------------------------------
        // 2. R3 BACK-END (Push-based States)
        // ----------------------------------------------------
        private readonly ReactiveProperty<float> _gravityRx = new();
        private readonly ReactiveProperty<float> _dampingRx = new();
        private readonly ReactiveProperty<float> _maxSwingAngleRx = new();
        private readonly ReactiveProperty<Vector3> _windVelocityRx = new();
        private readonly ReactiveProperty<float> _windTurbulenceRx = new();
        
        private DisposableBag _disposables;

        // --- Core Systems ---
        private GraphicsBuffer _argsBuffer;
        private GraphicsBuffer _chimeDataBuffer;
        
        private int _chimeCount;
        private int _kernelUpdate;
        private int _threadGroupsX; // Cached Dispatch Dimension
        private Bounds _globalBounds;

        private void OnEnable()
        {
            _disposables = new DisposableBag();
            
            InitializeSystem();
            InitializeReactivePipelines();
            ForceUpdateReactiveState();
        }

        private void InitializeReactivePipelines()
        {
            if (chimeCompute == null) return;

            // Zero-allocation stateful subscriptions pushing directly to the Compute Shader
            _gravityRx.DistinctUntilChanged().Subscribe(this, (v, state) => state.chimeCompute.SetFloat(Gravity, v)).AddTo(ref _disposables);
            _dampingRx.DistinctUntilChanged().Subscribe(this, (v, state) => state.chimeCompute.SetFloat(Damping, v)).AddTo(ref _disposables);
            
            // Mathematical transformation (Deg2Rad) happens ONCE when the property changes, not every frame
            _maxSwingAngleRx.DistinctUntilChanged().Subscribe(this, (v, state) => state.chimeCompute.SetFloat(MaxAngle, v * Mathf.Deg2Rad)).AddTo(ref _disposables);
            
            _windVelocityRx.DistinctUntilChanged().Subscribe(this, (v, state) => state.chimeCompute.SetVector(WindVelocity, v)).AddTo(ref _disposables);
            _windTurbulenceRx.DistinctUntilChanged().Subscribe(this, (v, state) => state.chimeCompute.SetFloat(WindTurbulence, v)).AddTo(ref _disposables);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            ForceUpdateReactiveState();
        }
#endif

        private void ForceUpdateReactiveState()
        {
            _gravityRx.Value = gravity;
            _dampingRx.Value = damping;
            _maxSwingAngleRx.Value = maxSwingAngle;
        }

        private void Update()
        {
            if (_chimeCount == 0 || _chimeDataBuffer == null || _argsBuffer == null) return;

            // 1. Wind Polling Firewall
            if (GlobalWeatherManager.Instance != null)
            {
                // R3 strictly intercepts these and drops the execution if the values haven't shifted.
                _windVelocityRx.Value = GlobalWeatherManager.Instance.CurrentWindVelocity;
                _windTurbulenceRx.Value = GlobalWeatherManager.Instance.CurrentWindTurbulence;
            }

            // 2. Pure Time Dispatches
            chimeCompute.SetFloat(DeltaTime, Time.unscaledDeltaTime);
            chimeCompute.SetFloat(Time1, Time.unscaledTime);

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
            chimeCompute.SetBuffer(_kernelUpdate, Chimes1, _chimeDataBuffer);
            chimeCompute.SetInt(ChimeCount, _chimeCount);
            instancedMaterial.SetBuffer($"_ChimeDataBuffer", _chimeDataBuffer);
        }

        private void OnDisable()
        {
            _disposables.Dispose();
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