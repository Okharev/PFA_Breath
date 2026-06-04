using UnityEngine;

namespace TechArtPlayground.Water
{
    [ExecuteAlways]
    public class OceanQuadtree : MonoBehaviour
    {
        [Header("Quadtree Settings")]
        [Tooltip("The main camera used for frustum culling.")]
        public Camera mainCamera; 
        public Transform viewer;
        public Material oceanMaterial;
        public float oceanSize = 8192f;
        public int maxDepth = 6;
        public float lodMultiplier = 2.5f;

        [Header("Optimization Settings")]
        [Tooltip("How often the Quadtree recalculates LODs (in seconds). 0.2 = 5 times a second.")]
        public float evaluationInterval = 0.2f;
        
        [Tooltip("Padding for the bounding box. Set this to your maximum wave height to prevent meshes popping out at the edges of the screen.")]
        public float maxWaveHeight = 15f; 

        [Header("Patch Settings")]
        public int patchResolution = 32;

        private Mesh patchMesh;
        private Matrix4x4[] instancedMatrices = new Matrix4x4[4096];
        private int currentInstanceCount = 0;
        private RenderParams renderParams;

        private float _timeSinceLastEvaluation;
        
        // Pre-allocated array for Zero-GC frustum extraction
        private Plane[] _frustumPlanes = new Plane[6];

        private void OnEnable()
        {
            if (mainCamera == null) mainCamera = Camera.main;

            GeneratePatchMesh(); 
            
            renderParams = new RenderParams(oceanMaterial);
            renderParams.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderParams.receiveShadows = true;

            _timeSinceLastEvaluation = evaluationInterval; 
        }

        private void Update()
        {
            if (viewer == null || oceanMaterial == null || patchMesh == null || mainCamera == null) return;

            // 1. TIME-SLICED DATA EVALUATION
            _timeSinceLastEvaluation += Time.deltaTime;
            
            if (_timeSinceLastEvaluation >= evaluationInterval)
            {
                _timeSinceLastEvaluation = 0f;
                currentInstanceCount = 0;

                // Extract frustum planes into our pre-allocated array (Zero GC)
                GeometryUtility.CalculateFrustumPlanes(mainCamera, _frustumPlanes);

                // Re-evaluate the tree
                EvaluateNodeFast(new Vector2(transform.position.x, transform.position.z), oceanSize, 0);
            }

            // 2. PER-FRAME RENDERING SUBMISSION
            if (currentInstanceCount > 0)
            {
                Graphics.RenderMeshInstanced(
                    renderParams, 
                    patchMesh, 
                    0, 
                    instancedMatrices, 
                    currentInstanceCount
                );
            }
        }

        // --- Optimized Quadtree Recursive Evaluation ---
        private void EvaluateNodeFast(Vector2 center, float size, int depth)
        {
            Vector3 nodeCenter3D = new Vector3(center.x, transform.position.y, center.y);
            
            // 1. FRUSTUM CULLING
            // Create a stack-allocated bounding box for this specific node
            Bounds nodeBounds = new Bounds(nodeCenter3D, new Vector3(size, maxWaveHeight * 2f, size));
            
            // If the bounding box is completely outside the camera's view, stop processing this branch entirely.
            if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, nodeBounds))
            {
                return; 
            }

            // 2. LOD DISTANCE CHECK
            Vector3 offset = viewer.position - nodeCenter3D;
            float sqrDistance = offset.sqrMagnitude;
            float lodThreshold = size * lodMultiplier;

            if (depth < maxDepth && sqrDistance < (lodThreshold * lodThreshold))
            {
                // Subdivide
                float quarterSize = size / 4f;
                float halfSize = size / 2f;
                int nextDepth = depth + 1;

                EvaluateNodeFast(center + new Vector2(-quarterSize, quarterSize), halfSize, nextDepth);
                EvaluateNodeFast(center + new Vector2(quarterSize, quarterSize), halfSize, nextDepth);
                EvaluateNodeFast(center + new Vector2(-quarterSize, -quarterSize), halfSize, nextDepth);
                EvaluateNodeFast(center + new Vector2(quarterSize, -quarterSize), halfSize, nextDepth);
            }
            else
            {
                // 3. ADD TO RENDER LIST
                if (currentInstanceCount < instancedMatrices.Length)
                {
                    instancedMatrices[currentInstanceCount++] = Matrix4x4.TRS(
                        nodeCenter3D, 
                        Quaternion.identity, 
                        new Vector3(size, 1f, size)
                    );
                }
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

    }
}

