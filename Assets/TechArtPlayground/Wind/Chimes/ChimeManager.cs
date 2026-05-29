using System.Runtime.InteropServices;
using UnityEngine;

namespace TechArtPlayground.Wind.Chimes
{
    [DefaultExecutionOrder(-50)]
    public class ComputeChimeSim : MonoBehaviour
    {
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

            chimeCompute.SetFloat("_DeltaTime", Time.deltaTime);
            chimeCompute.SetFloat("_Time", Time.time);
            chimeCompute.SetFloat("_Gravity", gravity);
            chimeCompute.SetFloat("_Damping", damping);

            // Convert degrees to radians for HLSL
            chimeCompute.SetFloat("_MaxAngle", maxSwingAngle * Mathf.Deg2Rad);

            // =========================================================
            // UPDATED: Read from the new WeatherManager
            // =========================================================
            Vector3 currentWindVel = WeatherManager.Instance != null
                ? WeatherManager.Instance.CurrentWindVelocity
                : Vector3.zero;
                
            float currentWindTurb = WeatherManager.Instance != null 
                ? WeatherManager.Instance.windGusts 
                : 0f;

            // Send the global wind to the Chimes Compute Shader
            chimeCompute.SetVector("_WindVelocity", currentWindVel);
            chimeCompute.SetFloat("_WindTurbulence", currentWindTurb);
            // =========================================================

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
            chimeCompute.SetInt("_ChimeCount", _chimeCount);

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