using System.Collections.Generic;
using UnityEngine;

namespace TechArtPlayground.Water
{
    [ExecuteAlways]
    public class OceanQuadtree : MonoBehaviour
    {
        [Header("Quadtree Settings")]
        public Transform viewer;
        public Material oceanMaterial;
    
        [Tooltip("Total size of the ocean (e.g., 8192 meters)")]
        public float oceanSize = 8192f;
    
        [Tooltip("How many times the root can subdivide")]
        public int maxDepth = 6;
    
        [Tooltip("Distance multiplier for LOD transitions. Higher = higher detail further away.")]
        public float lodMultiplier = 2.5f;

        [Header("Patch Settings")]
        [Tooltip("Vertices per side of the instanced patch (e.g., 16, 32, 64)")]
        public int patchResolution = 32;
        [Tooltip("How far down the edge skirts pull to hide T-Junction gaps")]
        public float skirtDepth = 2.0f;

        private Mesh patchMesh;
        private QuadTreeNode rootNode;
        private List<Matrix4x4> instancedMatrices = new List<Matrix4x4>(1023);

        // Bounding box for frustum culling
        private Bounds bounds;

        void OnEnable()
        {
            GeneratePatchMesh();
            bounds = new Bounds(Vector3.zero, new Vector3(oceanSize, 100f, oceanSize));
        }

        void Update()
        {
            if (viewer == null || oceanMaterial == null || patchMesh == null) return;

            instancedMatrices.Clear();

            // 1. Evaluate the Quadtree
            rootNode = new QuadTreeNode(
                new Vector2(transform.position.x, transform.position.z), 
                oceanSize, 
                0
            );
            EvaluateNode(rootNode);

            // 2. Render all patches in chunks of 1000
            if (instancedMatrices.Count > 0)
            {
                int maxInstances = 1000;
                for (int i = 0; i < instancedMatrices.Count; i += maxInstances)
                {
                    // Calculate how many matrices are left to draw in this chunk
                    int count = Mathf.Min(maxInstances, instancedMatrices.Count - i);
                
                    // Extract the chunk
                    List<Matrix4x4> chunk = instancedMatrices.GetRange(i, count);

                    // Dispatch the draw call safely
                    Graphics.DrawMeshInstanced(
                        patchMesh, 
                        0, 
                        oceanMaterial, 
                        chunk.ToArray(), 
                        count, 
                        null, 
                        UnityEngine.Rendering.ShadowCastingMode.Off, 
                        true
                    );
                }
            }
        }
        // --- Quadtree Recursive Evaluation ---
        private void EvaluateNode(QuadTreeNode node)
        {
            // Distance from camera to the center of this node
            Vector3 nodeCenter3D = new Vector3(node.center.x, transform.position.y, node.center.y);
            float distanceToViewer = Vector3.Distance(viewer.position, nodeCenter3D);

            // Subdivide if we are close enough, AND not at max depth
            // Condition: Distance is less than the node's size * our LOD multiplier
            if (node.depth < maxDepth && distanceToViewer < (node.size * lodMultiplier))
            {
                node.Subdivide();
                EvaluateNode(node.topLeft);
                EvaluateNode(node.topRight);
                EvaluateNode(node.bottomLeft);
                EvaluateNode(node.bottomRight);
            }
            else
            {
                // It's a leaf node. Add it to the render list.
                Matrix4x4 matrix = Matrix4x4.TRS(
                    nodeCenter3D, 
                    Quaternion.identity, 
                    new Vector3(node.size, 1f, node.size) // Scale the 1x1 patch to node size
                );
                instancedMatrices.Add(matrix);
            }
        }

        // --- Base Patch Generation (with Skirts) ---
        private void GeneratePatchMesh()
        {
            patchMesh = new Mesh { name = "Ocean_Quadtree_Patch" };
        
            int vertsPerSide = patchResolution + 1;
            // Add extra vertices for the 4 skirt edges
            int totalVerts = (vertsPerSide * vertsPerSide) + (vertsPerSide * 4);
        
            Vector3[] vertices = new Vector3[totalVerts];
            int[] triangles = new int[(patchResolution * patchResolution * 6) + (patchResolution * 24)];

            int vIndex = 0;
            int tIndex = 0;

            // 1. Generate Main Grid (Normalized from -0.5 to 0.5 so scaling it works perfectly)
            for (int z = 0; z < vertsPerSide; z++)
            {
                for (int x = 0; x < vertsPerSide; x++)
                {
                    float xPos = ((float)x / patchResolution) - 0.5f;
                    float zPos = ((float)z / patchResolution) - 0.5f;
                    vertices[vIndex++] = new Vector3(xPos, 0, zPos);
                }
            }

            // Generate Main Triangles
            for (int z = 0; z < patchResolution; z++)
            {
                for (int x = 0; x < patchResolution; x++)
                {
                    int current = x + (z * vertsPerSide);
                    int next = current + vertsPerSide;

                    triangles[tIndex++] = current;
                    triangles[tIndex++] = next;
                    triangles[tIndex++] = current + 1;

                    triangles[tIndex++] = current + 1;
                    triangles[tIndex++] = next;
                    triangles[tIndex++] = next + 1;
                }
            }

            // (For a production system, you append the skirt vertices here. 
            // Skirts map the outer ring of vertices, duplicate them, and set Y = -skirtDepth.
            // For brevity in this script, standard bounds are used).

            patchMesh.vertices = vertices;
            patchMesh.triangles = triangles;
            patchMesh.RecalculateNormals();
        
            // Massive bounds prevent frustum culling issues when vertices are displaced by FFT
            patchMesh.bounds = new Bounds(Vector3.zero, new Vector3(100f, 100f, 100f));
        }

        // --- Quadtree Node Structure ---
        private class QuadTreeNode
        {
            public Vector2 center;
            public float size;
            public int depth;

            public QuadTreeNode topLeft, topRight, bottomLeft, bottomRight;

            public QuadTreeNode(Vector2 center, float size, int depth)
            {
                this.center = center;
                this.size = size;
                this.depth = depth;
            }

            public void Subdivide()
            {
                float quarterSize = size / 4f;
                float halfSize = size / 2f;
                int nextDepth = depth + 1;

                topLeft = new QuadTreeNode(center + new Vector2(-quarterSize, quarterSize), halfSize, nextDepth);
                topRight = new QuadTreeNode(center + new Vector2(quarterSize, quarterSize), halfSize, nextDepth);
                bottomLeft = new QuadTreeNode(center + new Vector2(-quarterSize, -quarterSize), halfSize, nextDepth);
                bottomRight = new QuadTreeNode(center + new Vector2(quarterSize, -quarterSize), halfSize, nextDepth);
            }
        }
    }
}