using System.Collections.Generic;
using UnityEngine;

namespace ChimeTest
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer))]
    public class ProceduralChimeGenerator : MonoBehaviour
    {
        [Header("Placement")]
        public Transform pointA;
        public Transform pointB;
        [Range(0.1f, 5f)] public float sagAmount = 1.5f;
        public int chimeCount = 12;

        [Header("Materials (Auto-Assign)")]
        [Tooltip("The material using your Custom/URP/ProceduralChimes shader.")]
        public Material chimeMaterial;
        [Tooltip("The material using your Custom/URP/RopeWind shader.")]
        public Material ropeMaterial;

        [Header("Rope Rendering")]
        public float ropeWidth = 0.02f;
        [Range(10, 50)] public int ropeResolution = 20;

        [Header("Chime Assets")]
        public ChimeData[] availableChimes;

        [System.Serializable]
        public struct ChimeData
        {
            public MeshFilter meshPrefab;
            [Range(0f, 1f)] public float rigidity; // 0 = Soft Ribbon, 1 = Rigid Bell
            [Range(0.1f, 5f)] public float weight; // Higher = slower/less swing
        }

        [ContextMenu("Generate Chimes & Rope")]
        public void Generate()
        {
            if (pointA == null || pointB == null || availableChimes.Length == 0)
            {
                Debug.LogWarning("Missing references in Chime Generator.");
                return;
            }

            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;

            // ==========================================
            // 1. GENERATE THE ROPE (Line Renderer)
            // ==========================================
            LineRenderer ropeRenderer = GetComponent<LineRenderer>();
            ropeRenderer.useWorldSpace = false; 
            ropeRenderer.startWidth = ropeWidth;
            ropeRenderer.endWidth = ropeWidth;
            ropeRenderer.positionCount = ropeResolution;

            for (int i = 0; i < ropeResolution; i++)
            {
                float t = (float)i / (Mathf.Max(1, ropeResolution - 1));
                Vector3 worldPos = EvaluateCatenary(pointA.position, pointB.position, t, sagAmount);
                Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos);
                ropeRenderer.SetPosition(i, localPos);
            }

            // Auto-assign the rope material
            if (ropeMaterial != null)
            {
                ropeRenderer.sharedMaterial = ropeMaterial;
            }

            // ==========================================
            // 2. GENERATE THE CHIMES (Combined Mesh)
            // ==========================================
            List<CombineInstance> combineInstances = new List<CombineInstance>();
        
            for (int i = 0; i < chimeCount; i++)
            {
                float t = (float)i / (Mathf.Max(1, chimeCount - 1));
            
                Vector3 worldPos = EvaluateCatenary(pointA.position, pointB.position, t, sagAmount);
                Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos);

                ChimeData chime = availableChimes[Random.Range(0, availableChimes.Length)];
                if (chime.meshPrefab == null) continue;

                Mesh sourceMesh = chime.meshPrefab.sharedMesh;
                Mesh bakedMesh = Instantiate(sourceMesh); 

                Vector3[] vertices = bakedMesh.vertices;
                Color[] colors = new Color[vertices.Length];
                List<Vector4> uv2 = new List<Vector4>(vertices.Length);

// NEW: Grab the existing colors and check if the mesh is already baked
                Color[] sourceColors = sourceMesh.colors;
                bool hasBakedColors = sourceColors != null && sourceColors.Length == vertices.Length;

                float minY = bakedMesh.bounds.min.y;
                float height = bakedMesh.bounds.size.y;

                for (int v = 0; v < vertices.Length; v++)
                {
                    if (hasBakedColors)
                    {
                        // 1. PRESERVE the expertly baked data from your Bell Generator tool!
                        colors[v] = sourceColors[v];
                    }
                    else
                    {
                        // 2. FALLBACK for generic 3D models dropped into the array
                        float normalizedY = height > 0 ? (vertices[v].y - minY) / height : 0;
                        float verticalMask = 1.0f - normalizedY; 
        
                        // Relies on the Inspector sliders (Rigidity/Weight) on this script
                        colors[v] = new Color(verticalMask, chime.rigidity, chime.weight, 1.0f);
                    }
                    // Pack 't' (0.0 to 1.0 along the rope) into the W component!
                    uv2.Add(new Vector4(localPos.x, localPos.y, localPos.z, t));
                }

                bakedMesh.colors = colors;
                bakedMesh.SetUVs(2, uv2);

                CombineInstance ci = new CombineInstance();
                ci.mesh = bakedMesh;
                ci.transform = Matrix4x4.Translate(localPos); 
                
                
                
                combineInstances.Add(ci);
            }

            Mesh finalMesh = new Mesh();
            finalMesh.name = "Combined_Procedural_Chimes";
            finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            finalMesh.CombineMeshes(combineInstances.ToArray(), true, true);

            GetComponent<MeshFilter>().sharedMesh = finalMesh;

            // Auto-assign the combined chime material
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (chimeMaterial != null)
            {
                meshRenderer.sharedMaterial = chimeMaterial;
            }

            foreach (var ci in combineInstances)
            {
                DestroyImmediate(ci.mesh);
            }

            Debug.Log($"Generated {chimeCount} chimes and an active LineRenderer rope.");
        }


        private Vector3 EvaluateCatenary(Vector3 p0, Vector3 p1, float t, float sag)
        {
            Vector3 linearLerp = Vector3.Lerp(p0, p1, t);
        
            // Remap t from [0, 1] to [-1, 1] to center the hyperbolic curve
            float x = (t - 0.5f) * 2.0f; 
        
            // Fix: Manually calculate Cosh using Mathf.Exp
            float coshx = (Mathf.Exp(x * sag) + Mathf.Exp(-x * sag)) * 0.5f;
            float coshsag = (Mathf.Exp(sag) + Mathf.Exp(-sag)) * 0.5f;
        
            float catenaryY = coshx - coshsag;
        
            // Apply sag depth
            return linearLerp + (Vector3.up * catenaryY);
        }
    }
}
