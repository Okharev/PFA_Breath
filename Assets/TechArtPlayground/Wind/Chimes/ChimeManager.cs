using System.Runtime.InteropServices;
using UnityEngine;

namespace TechArtPlayground.Wind.Chimes
{
    [DefaultExecutionOrder(-50)]
    public class ComputeChimeSim : MonoBehaviour
    {
        private static readonly int ChimeCount = Shader.PropertyToID("_ChimeCount");
        private static readonly int WindVelocity = Shader.PropertyToID("_WindVelocity");
        private static readonly int WindTurbulence = Shader.PropertyToID("_WindTurbulence");
        private static readonly int DeltaTime = Shader.PropertyToID("_DeltaTime");
        private static readonly int Time1 = Shader.PropertyToID("_Time");
        private static readonly int Gravity = Shader.PropertyToID("_Gravity");
        private static readonly int Damping = Shader.PropertyToID("_Damping");
        private static readonly int MaxAngle = Shader.PropertyToID("_MaxAngle");
        [Header("References")] public ComputeShader chimeCompute;

        public Material instancedMaterial;
        public Mesh chimeMesh;

        [Header("Environment Physics")] public float gravity = 9.81f;

        public float damping = 0.5f;

        [Header("Constraints")]
        [Tooltip("Angle maximum en degrés avant que le carillon ne soit bloqué.")]
        [Range(10f, 170f)]
        public float maxSwingAngle = 80f;

        private GraphicsBuffer _argsBuffer;
        private int _chimeCount;

        private GraphicsBuffer _chimeDataBuffer;
        private Bounds _globalBounds;
        private int _kernelUpdate;

        private void Start()
        {
            InitializeSystem();
        }

        private void Update()
        {
            if (_chimeCount == 0 || _chimeDataBuffer == null || _argsBuffer == null) return;

            chimeCompute.SetFloat(DeltaTime, Time.unscaledDeltaTime);
            chimeCompute.SetFloat(Time1, Time.unscaledTime);
            chimeCompute.SetFloat(Gravity, gravity);
            chimeCompute.SetFloat(Damping, damping);
            chimeCompute.SetFloat(MaxAngle, maxSwingAngle * Mathf.Deg2Rad);

            // =========================================================
            // UPDATED: Read from the new GlobalWeatherManager
            // =========================================================
            Vector3 currentWindVel = Vector3.zero;
            float currentWindTurb = 0f;

            if (GlobalWeatherManager.Instance != null)
            {
                currentWindVel = GlobalWeatherManager.Instance.CurrentWindVelocity;
                currentWindTurb = GlobalWeatherManager.Instance.CurrentWindTurbulence;
            }

            // Send the global wind to the Chimes Compute Shader
            chimeCompute.SetVector(WindVelocity, currentWindVel);
            chimeCompute.SetFloat(WindTurbulence, currentWindTurb);

            int threadGroupsX = Mathf.CeilToInt(_chimeCount / 64f);
            chimeCompute.Dispatch(_kernelUpdate, threadGroupsX, 1, 1);

            Graphics.DrawMeshInstancedIndirect(chimeMesh, 0, instancedMaterial, _globalBounds, _argsBuffer);
        }

        private void OnDestroy()
        {
            _chimeDataBuffer?.Dispose();
            _argsBuffer?.Dispose();
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

            int stride = Marshal.SizeOf(typeof(ChimeData));
            _chimeDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _chimeCount, stride);
            _chimeDataBuffer.SetData(chimeDataArray); // Envoi des données initiales !

            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            args[0] = chimeMesh.GetIndexCount(0);
            args[1] = (uint)_chimeCount;
            args[2] = chimeMesh.GetIndexStart(0);
            args[3] = chimeMesh.GetBaseVertex(0);

            _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, args.Length * sizeof(uint));
            _argsBuffer.SetData(args);

            _kernelUpdate = chimeCompute.FindKernel("CSUpdateChimes");
            chimeCompute.SetBuffer(_kernelUpdate, "Chimes", _chimeDataBuffer);
            chimeCompute.SetInt(ChimeCount, _chimeCount);

            instancedMaterial.SetBuffer("_ChimeDataBuffer", _chimeDataBuffer);
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