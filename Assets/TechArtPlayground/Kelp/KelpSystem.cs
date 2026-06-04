// ==========================================
// FILE: KelpSystem.cs
// ==========================================

using System.Runtime.InteropServices;
using UnityEngine;

namespace TechArtPlayground.Kelp
{
    public class KelpSystem : MonoBehaviour
    {
        [Header("Dependencies")]
        public ComputeShader kelpCompute;
        public Material instancedKelpMaterial;

        [Header("Environment")]
        public Vector3 windDirection = new Vector3(1, 0, 0);
        public float windSpeed = 2.0f;
        public Vector3 gravityDir = new Vector3(0, 1, 0);
        public float stalkSegmentLength = 0.5f;
        public float leafSegmentLength = 0.3f;
        
        public Material instancedStalkMaterial;
        private Mesh stalkMesh;
        private GraphicsBuffer argsBufferStalks;
        [System.Serializable]
        public class KelpProfile
        {
            public string name = "Kelp Type";
            [ColorUsage(true, true)] public Color colorBase = Color.green;
            [ColorUsage(true, true)] public Color colorTip = Color.yellow;
            public float windStrength = 1.5f;
            public float windScale = 0.5f;
            public float gravity = 1.0f;
            public float leafScale = 1.0f;
            public float stalkThickness = 0.1f;
        }

        [Header("Profiles & Spawning")]
        public KelpProfile[] kelpTypes;
        public int totalStalks = 100;
        public int nodesPerStalk = 10;
        public int leavesPerStalk = 15; // To simplify, uniform distribution for this example

        // --- DATA STRUCTS (16-byte aligned for HLSL) ---
        [StructLayout(LayoutKind.Sequential)]
        private struct KelpTypeData {
            public Vector4 colorBase;
            public Vector4 colorTip;
            public float windStrength;
            public float windScale;
            public float gravity;
            public float leafScale;
            public float stalkThickness;
            public Vector3 padding; // Exactly 64 bytes
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StalkNodeData {
            public Vector3 position;
            public Vector3 prevPosition;
            public Vector3 normal;
            public Vector3 tangent;
            public int typeIndex;
            public Vector3 padding; // Exactly 64 bytes
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LeafObjectData {
            public int stalkNodeIndex;
            public int leafNodeStartIndex;
            public int typeIndex;
            public float colorGradientLerp;
            public Vector4 restRotation;
            public float coneAngle;
            public Vector3 padding; // Exactly 48 bytes
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LeafNodeData {
            public Vector3 position;
            public Vector3 prevPosition;
            public Vector2 padding; // Exactly 32 bytes
        }
        // --- INTERNAL BUFFERS ---
        private ComputeBuffer typeBuffer;
        private ComputeBuffer stalkNodeBuffer;
        private ComputeBuffer leafObjectBuffer;
        private ComputeBuffer leafNodeBuffer;
        private GraphicsBuffer argsBuffer;

        private Mesh leafMesh;
        private int kernelUpdateStalks;
        private int kernelUpdateLeaves;

        private void Start()
        {
            leafMesh = KelpGenerator.GenerateLeafMesh();
            kernelUpdateStalks = kelpCompute.FindKernel("UpdateStalks");
            kernelUpdateLeaves = kelpCompute.FindKernel("UpdateLeaves");

            InitializeBuffers();
        }

        private void InitializeBuffers()
    {
        // 1. Initialize Type Buffer
        KelpTypeData[] typeDataArray = new KelpTypeData[kelpTypes.Length];
        for (int i = 0; i < kelpTypes.Length; i++)
        {
            typeDataArray[i] = new KelpTypeData {
                colorBase = kelpTypes[i].colorBase,
                colorTip = kelpTypes[i].colorTip,
                windStrength = kelpTypes[i].windStrength,
                windScale = kelpTypes[i].windScale,
                gravity = kelpTypes[i].gravity,
                leafScale = kelpTypes[i].leafScale,
                stalkThickness = kelpTypes[i].stalkThickness,
                padding = Vector3.zero
            };
        }
        typeBuffer = new ComputeBuffer(kelpTypes.Length, Marshal.SizeOf<KelpTypeData>());
        typeBuffer.SetData(typeDataArray);

        // 2. Initialize Stalks and Leaves Arrays
        int totalStalkNodes = totalStalks * nodesPerStalk;
        
        // FIX: Calculate EXACTLY how many leaves we will spawn (1 per node, skipping the root)
        int actualLeavesPerStalk = (nodesPerStalk - 1); 
        int totalLeaves = totalStalks * actualLeavesPerStalk;
        int totalLeafNodes = totalLeaves * 3; 

        StalkNodeData[] stalkArray = new StalkNodeData[totalStalkNodes];
        LeafObjectData[] leafObjArray = new LeafObjectData[totalLeaves];
        LeafNodeData[] leafNodeArray = new LeafNodeData[totalLeafNodes];

        int currentLeafIdx = 0;
        int currentLeafNodeIdx = 0;

        for (int i = 0; i < totalStalks; i++)
        {
            int typeIdx = Random.Range(0, kelpTypes.Length);
            // Spread stalks out over a 40x40 meter area so they aren't clumped
            Vector3 startPos = new Vector3(Random.Range(-20f, 20f), 0, Random.Range(-20f, 20f));

            for (int n = 0; n < nodesPerStalk; n++)
            {
                int stalkNodeIdx = i * nodesPerStalk + n;
                Vector3 nodePos = startPos + Vector3.up * (n * stalkSegmentLength);
                
                stalkArray[stalkNodeIdx] = new StalkNodeData {
                    position = nodePos,
                    prevPosition = nodePos,
                    typeIndex = typeIdx,
                    padding = Vector3.zero
                };

                // FIX: Spawn exactly one leaf for every node except the root (n > 0)
                if (n > 0) 
                {
                    // Rotate the leaves so they spiral up the stalk organically
                    float spiralAngle = n * 137.5f; // Golden ratio spiral
                    Quaternion restRot = Quaternion.Euler(Random.Range(-20f, 20f), spiralAngle, 0);

                    leafObjArray[currentLeafIdx] = new LeafObjectData {
                        stalkNodeIndex = stalkNodeIdx,
                        leafNodeStartIndex = currentLeafNodeIdx,
                        typeIndex = typeIdx,
                        colorGradientLerp = Random.value,
                        restRotation = new Vector4(restRot.x, restRot.y, restRot.z, restRot.w),
                        coneAngle = 0.5f,
                        padding = Vector3.zero
                    };

                    for (int ln = 0; ln < 3; ln++) {
                        // Push leaves outward based on their rest rotation so they don't start at origin
                        Vector3 outwardDir = restRot * Vector3.right;
                        Vector3 leafPos = nodePos + (outwardDir * ln * leafSegmentLength);
                        
                        leafNodeArray[currentLeafNodeIdx + ln] = new LeafNodeData {
                            position = leafPos,
                            prevPosition = leafPos,
                            padding = Vector2.zero
                        };
                    }
                    
                    currentLeafIdx++;
                    currentLeafNodeIdx += 3;
                }
            }
        }

        stalkNodeBuffer = new ComputeBuffer(totalStalkNodes, Marshal.SizeOf<StalkNodeData>());
        stalkNodeBuffer.SetData(stalkArray);

        leafObjectBuffer = new ComputeBuffer(totalLeaves, Marshal.SizeOf<LeafObjectData>());
        leafObjectBuffer.SetData(leafObjArray);

        leafNodeBuffer = new ComputeBuffer(totalLeafNodes, Marshal.SizeOf<LeafNodeData>());
        leafNodeBuffer.SetData(leafNodeArray);

        // 3. Setup Graphics Buffer for Instancing
// Setup Leaf Args
        uint[] args = new uint[5];
        args[0] = (uint)leafMesh.GetIndexCount(0);
        args[1] = (uint)totalLeaves;
        args[2] = (uint)leafMesh.GetIndexStart(0);
        args[3] = (uint)leafMesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, args.Length * sizeof(uint));
        argsBuffer.SetData(args);

        // ---> ADD STALK ARGS HERE <---
        stalkMesh = KelpGenerator.GenerateStalkMesh();
        uint totalStalkSegments = (uint)(totalStalks * (nodesPerStalk - 1));
        uint[] stalkArgs = new uint[5];
        stalkArgs[0] = (uint)stalkMesh.GetIndexCount(0);
        stalkArgs[1] = totalStalkSegments;
        stalkArgs[2] = (uint)stalkMesh.GetIndexStart(0);
        stalkArgs[3] = (uint)stalkMesh.GetBaseVertex(0);
        stalkArgs[4] = 0;
        argsBufferStalks = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, stalkArgs.Length * sizeof(uint));
        argsBufferStalks.SetData(stalkArgs);

        BindBuffers();
    }

        private void BindBuffers()
        {
            // Compute Shader Bindings
            kelpCompute.SetBuffer(kernelUpdateStalks, "_KelpTypes", typeBuffer);
            kelpCompute.SetBuffer(kernelUpdateStalks, "_StalkNodes", stalkNodeBuffer);

            kelpCompute.SetBuffer(kernelUpdateLeaves, "_StalkNodes", stalkNodeBuffer);
            kelpCompute.SetBuffer(kernelUpdateLeaves, "_LeafObjects", leafObjectBuffer);
            kelpCompute.SetBuffer(kernelUpdateLeaves, "_LeafNodes", leafNodeBuffer);
            
            instancedStalkMaterial.SetBuffer("_KelpTypes", typeBuffer);
            instancedStalkMaterial.SetBuffer("_StalkNodes", stalkNodeBuffer);
            instancedKelpMaterial.SetBuffer("_LeafObjects", leafObjectBuffer);
            instancedKelpMaterial.SetBuffer("_LeafNodes", leafNodeBuffer);
        }

        private void Update()
        {
            if (stalkNodeBuffer == null) return; // Safety check

            // 1. Pass Uniforms
            kelpCompute.SetFloat("_Time", Time.time);
            kelpCompute.SetFloat("_DeltaTime", Time.deltaTime);
            kelpCompute.SetVector("_UpwardGravity", gravityDir);
            kelpCompute.SetVector("_WindDirection", windDirection.normalized);
            kelpCompute.SetFloat("_WindSpeed", windSpeed);
            kelpCompute.SetFloat("_StalkSegmentLength", stalkSegmentLength);
            kelpCompute.SetFloat("_LeafSegmentLength", leafSegmentLength);
        
            // FIX: Pass boundary counts to prevent thread crashes
            kelpCompute.SetInt("_TotalStalkNodes", totalStalks * nodesPerStalk);
            kelpCompute.SetInt("_TotalLeaves", leafObjectBuffer.count);
            kelpCompute.SetInt("_NodesPerStalk", nodesPerStalk);
            
            Shader.SetGlobalInt("_NodesPerStalk", nodesPerStalk);

            // 2. Dispatch Compute
            int stalkGroups = Mathf.CeilToInt((totalStalks * nodesPerStalk) / 64f);
            kelpCompute.Dispatch(kernelUpdateStalks, stalkGroups, 1, 1);

            int leafGroups = Mathf.CeilToInt(leafObjectBuffer.count / 64f);
            kelpCompute.Dispatch(kernelUpdateLeaves, leafGroups, 1, 1);

            // 3. Render Indirect
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(500, 500, 500));
        
            // Draw Leaves
            Graphics.DrawMeshInstancedIndirect(leafMesh, 0, instancedKelpMaterial, bounds, argsBuffer);
            // ---> ADD: Draw Stalks <---
            Graphics.DrawMeshInstancedIndirect(stalkMesh, 0, instancedStalkMaterial, bounds, argsBufferStalks);
        }

        private void OnDestroy()
        {
            // Prevent memory leaks
            typeBuffer?.Release();
            stalkNodeBuffer?.Release();
            leafObjectBuffer?.Release();
            leafNodeBuffer?.Release();
            argsBuffer?.Release();
            argsBufferStalks?.Release();
        }
    }
}