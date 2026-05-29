using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChimeTest.Editor
{
    public class BellChimeGenerator : EditorWindow
    {
        [Header("Mesh Topology")]
        public float height = 0.5f;
        public float topRadius = 0.02f;
        public float bottomRadius = 0.15f;
        [Range(0.1f, 5f)] public float bellFlare = 2.0f; // Curve of the bell
        [Range(3, 32)] public int radialSegments = 12;
        [Range(1, 16)] public int heightSegments = 8; // Higher = better soft bending

        [Header("Baked Shader Data")]
        [Range(0f, 1f)] public float rigidity = 1.0f; // 1.0 = Rigid Bell
        [Range(0.1f, 5f)] public float weight = 2.5f; 
    
        [Header("Export")]
        public string savePath = "Assets/ProceduralBell.asset";

        [MenuItem("Tools/Graphics/Bell Chime Generator")]
        public static void ShowWindow()
        {
            GetWindow<BellChimeGenerator>("Bell Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Procedural Bell Generation", EditorStyles.boldLabel);
        
            EditorGUILayout.Space();
        
            SerializedObject so = new SerializedObject(this);
            so.Update();

            EditorGUILayout.PropertyField(so.FindProperty("height"));
            EditorGUILayout.PropertyField(so.FindProperty("topRadius"));
            EditorGUILayout.PropertyField(so.FindProperty("bottomRadius"));
            EditorGUILayout.PropertyField(so.FindProperty("bellFlare"), new GUIContent("Flare Curve (Power)"));
            EditorGUILayout.PropertyField(so.FindProperty("radialSegments"));
            EditorGUILayout.PropertyField(so.FindProperty("heightSegments"));

            EditorGUILayout.Space();
            GUILayout.Label("Shader Packing Data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("rigidity"));
            EditorGUILayout.PropertyField(so.FindProperty("weight"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(so.FindProperty("savePath"));

            so.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate & Save Mesh", GUILayout.Height(30)))
            {
                GenerateMesh();
            }
        }

        private void GenerateMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Procedural_Bell";

            int vertexCount = (radialSegments + 1) * (heightSegments + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uv0 = new Vector2[vertexCount];
            List<Vector3> uv2 = new List<Vector3>(vertexCount);
            Color[] colors = new Color[vertexCount];

            // The pivot of this generated prefab mesh is its local origin (0,0,0)
            // Which is exactly where it attaches to the rope.
            Vector3 localPivot = Vector3.zero;

            int vertIndex = 0;

            for (int y = 0; y <= heightSegments; y++)
            {
                float v = (float)y / heightSegments; // 0.0 at top, 1.0 at bottom
                float currentHeight = -v * height; // Built downwards from origin

                // Flare math: use a power function to make it curve outward like a bell
                float curveT = Mathf.Pow(v, bellFlare);
                float currentRadius = Mathf.Lerp(topRadius, bottomRadius, curveT);

                for (int x = 0; x <= radialSegments; x++)
                {
                    float u = (float)x / radialSegments;
                    float angle = u * Mathf.PI * 2.0f;

                    float sin = Mathf.Sin(angle);
                    float cos = Mathf.Cos(angle);

                    // Vertex Position
                    Vector3 pos = new Vector3(cos * currentRadius, currentHeight, sin * currentRadius);
                    vertices[vertIndex] = pos;

                    // Standard UV0
                    uv0[vertIndex] = new Vector2(u, v);

                    // Shader Data Packing
                    // Color.R = Vertical Mask (0 at top, 1 at bottom)
                    // Color.G = Rigidity
                    // Color.B = Weight/Inertia
                    colors[vertIndex] = new Color(v, rigidity, weight, 1.0f);

                    // UV2 = Object Space Pivot
                    uv2.Add(localPivot);

                    // Calculate approximate normals (pointing outward and slightly up/down based on flare)
                    Vector3 normal = new Vector3(cos, (bottomRadius - topRadius) / height, sin).normalized;
                    normals[vertIndex] = normal;

                    vertIndex++;
                }
            }

            // Triangles
// Triangles
            int quadCount = radialSegments * heightSegments;
            int[] triangles = new int[quadCount * 6];
            int triIndex = 0;

            for (int y = 0; y < heightSegments; y++)
            {
                for (int x = 0; x < radialSegments; x++)
                {
                    int current = y * (radialSegments + 1) + x;
                    int next = current + 1;
                    int down = current + (radialSegments + 1); // Note: renamed 'up' to 'down' for clarity!
                    int downNext = down + 1;

                    // Flipped winding order for OUTWARD facing geometry (Clockwise)
                    // Triangle 1
                    triangles[triIndex++] = current;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = down;

                    // Triangle 2
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = downNext;
                    triangles[triIndex++] = down;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv0;
            mesh.SetUVs(2, uv2);
            mesh.colors = colors;
            mesh.triangles = triangles;

            mesh.RecalculateBounds();
            // Optional: Recalculate normals accurately based on triangles if our approximation isn't perfectly smooth
            mesh.RecalculateNormals(); 

            SaveMeshAsset(mesh);
        }

        private void SaveMeshAsset(Mesh mesh)
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create or overwrite the asset
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(savePath);
            if (existingMesh != null)
            {
                existingMesh.Clear();
                EditorUtility.CopySerialized(mesh, existingMesh);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=cyan>[Bell Generator]</color> Overwrote existing mesh at {savePath}");
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, savePath);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=cyan>[Bell Generator]</color> Created new mesh at {savePath}");
            }

            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Mesh>(savePath));
        }
    }
}