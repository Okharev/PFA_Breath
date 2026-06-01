// ==========================================
// FILE: KelpGenerator.cs
// ==========================================

using UnityEngine;

namespace TechArtPlayground.Kelp
{
    public static class KelpGenerator 
    {
        // Generates a low-poly diamond shape leaf with the pivot exactly at the base
        public static Mesh GenerateLeafMesh(float width = 0.2f, float length = 1.0f)
        {
            Mesh mesh = new Mesh { name = "ProceduralKelpLeaf" };

            Vector3[] vertices = new Vector3[5]
            {
                new Vector3(0, 0, 0),                       // 0: Base (Pivot)
                new Vector3(-width * 0.5f, 0, length * 0.3f), // 1: Left width
                new Vector3(width * 0.5f, 0, length * 0.3f),  // 2: Right width
                new Vector3(0, 0, length),                  // 3: Tip
                new Vector3(0, width * 0.2f, length * 0.3f) // 4: Center Ridge for volume
            };

            Vector2[] uvs = new Vector2[5]
            {
                new Vector2(0.5f, 0.0f),
                new Vector2(0.0f, 0.3f),
                new Vector2(1.0f, 0.3f),
                new Vector2(0.5f, 1.0f),
                new Vector2(0.5f, 0.3f)
            };

            int[] triangles = new int[]
            {
                // Top side
                0, 4, 1,   0, 2, 4,   1, 4, 3,   4, 2, 3,
                // Bottom side
                0, 1, 2,   1, 3, 2
            };

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
        
        public static Mesh GenerateStalkMesh()
        {
            Mesh mesh = new Mesh { name = "ProceduralKelpStalk" };
        
            // 4-sided tube, pivot at bottom (0,0,0), extending to Z=1
            Vector3[] vertices = new Vector3[] {
                new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0),
                new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1)
            };

            int[] triangles = new int[] {
                0,4,5, 0,5,1, // Bottom
                1,5,6, 1,6,2, // Right
                2,6,7, 2,7,3, // Top
                3,7,4, 3,4,0  // Left
            };

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}