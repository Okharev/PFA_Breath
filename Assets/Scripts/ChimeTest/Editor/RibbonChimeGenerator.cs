using UnityEngine;
using UnityEditor;
using System.IO;

namespace ChimeTest.Editor
{
    public class RibbonChimeGenerator : EditorWindow
    {
        [Header("Ribbon Dimensions")]
        public float width = 0.05f;
        public float length = 0.6f;
        [Tooltip("Microscopic thickness prevents Z-fighting when rendering both sides.")]
        public float thickness = 0.001f; 
        
        [Tooltip("Horizontal cuts. 1 is usually enough for a flat ribbon.")]
        [Range(1, 8)] public int widthSegments = 1; 
        
        [Tooltip("Vertical cuts. Higher means smoother bending in the wind.")]
        [Range(4, 32)] public int lengthSegments = 12;

        [Header("Baked Shader Data")]
        [Range(0f, 1f)] public float rigidity = 0.0f; 
        [Range(0.05f, 2f)] public float weight = 0.2f; 
        
        [Header("Export")]
        public string savePath = "Assets/ProceduralRibbon.asset";

        [MenuItem("Tools/Graphics/Ribbon Chime Generator")]
        public static void ShowWindow()
        {
            GetWindow<RibbonChimeGenerator>("Ribbon Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Double-Sided Ribbon Generation", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            SerializedObject so = new SerializedObject(this);
            so.Update();

            EditorGUILayout.PropertyField(so.FindProperty("width"));
            EditorGUILayout.PropertyField(so.FindProperty("length"));
            EditorGUILayout.PropertyField(so.FindProperty("thickness"));
            EditorGUILayout.PropertyField(so.FindProperty("widthSegments"));
            EditorGUILayout.PropertyField(so.FindProperty("lengthSegments"));

            EditorGUILayout.Space();
            GUILayout.Label("Shader Packing Data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("rigidity"), new GUIContent("Rigidity (0 = Cloth)"));
            EditorGUILayout.PropertyField(so.FindProperty("weight"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(so.FindProperty("savePath"));

            so.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate & Save Ribbon", GUILayout.Height(30)))
            {
                GenerateRibbon();
            }
        }

        private void GenerateRibbon()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Procedural_Ribbon_DoubleSided";

            // We generate exactly two faces for every point
            int vertsPerFace = (widthSegments + 1) * (lengthSegments + 1);
            int vertexCount = vertsPerFace * 2;
            
            // Using precise arrays instead of Lists for performance
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uv0 = new Vector2[vertexCount];
            Vector3[] uv2 = new Vector3[vertexCount];
            Color[] colors = new Color[vertexCount];

            Vector3 localPivot = Vector3.zero;
            float halfThickness = thickness * 0.5f;

            for (int y = 0; y <= lengthSegments; y++)
            {
                float v = (float)y / lengthSegments; 
                float currentHeight = -v * length;

                for (int x = 0; x <= widthSegments; x++)
                {
                    float u = (float)x / widthSegments; 
                    float currentWidth = (u - 0.5f) * width;
                    
                    int vFront = y * (widthSegments + 1) + x;
                    int vBack = vFront + vertsPerFace;

                    // ==========================================
                    // FRONT FACE (-Z direction)
                    // ==========================================
                    vertices[vFront] = new Vector3(currentWidth, currentHeight, -halfThickness);
                    normals[vFront] = new Vector3(0, 0, -1);
                    uv0[vFront] = new Vector2(u, v);
                    uv2[vFront] = localPivot;
                    colors[vFront] = new Color(v, rigidity, weight, 1.0f);

                    // ==========================================
                    // BACK FACE (+Z direction)
                    // ==========================================
                    vertices[vBack] = new Vector3(currentWidth, currentHeight, halfThickness);
                    normals[vBack] = new Vector3(0, 0, 1);
                    // Pro-Tip: We invert the 'U' coordinate on the backface (1.0 - u) 
                    // so textures/patterns don't look mirrored or backwards!
                    uv0[vBack] = new Vector2(1.0f - u, v); 
                    uv2[vBack] = localPivot;
                    colors[vBack] = new Color(v, rigidity, weight, 1.0f);
                }
            }

            // Triangles
            int quadCountPerFace = widthSegments * lengthSegments;
            int[] triangles = new int[quadCountPerFace * 6 * 2]; 
            int triIndex = 0;

            for (int y = 0; y < lengthSegments; y++)
            {
                for (int x = 0; x < widthSegments; x++)
                {
                    // Indices for the front
                    int currentFront = y * (widthSegments + 1) + x;
                    int nextFront = currentFront + 1;
                    int downFront = currentFront + (widthSegments + 1);
                    int downNextFront = downFront + 1;

                    // Front Face Winding (Clockwise)
                    triangles[triIndex++] = currentFront;
                    triangles[triIndex++] = nextFront;
                    triangles[triIndex++] = downFront;

                    triangles[triIndex++] = nextFront;
                    triangles[triIndex++] = downNextFront;
                    triangles[triIndex++] = downFront;

                    // Indices for the back
                    int currentBack = vertsPerFace + currentFront;
                    int nextBack = vertsPerFace + nextFront;
                    int downBack = vertsPerFace + downFront;
                    int downNextBack = vertsPerFace + downNextFront;

                    // Back Face Winding (Counter-Clockwise to face +Z)
                    triangles[triIndex++] = currentBack;
                    triangles[triIndex++] = downBack;
                    triangles[triIndex++] = nextBack;

                    triangles[triIndex++] = nextBack;
                    triangles[triIndex++] = downBack;
                    triangles[triIndex++] = downNextBack;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv0;
            mesh.SetUVs(2, uv2);
            mesh.colors = colors;
            mesh.triangles = triangles;

            mesh.RecalculateBounds();
            // Expand the bounds so Unity doesn't cull the mesh when the wind blows it hard horizontally
            Bounds bounds = mesh.bounds;
            bounds.Expand(length * 0.75f);
            mesh.bounds = bounds;

            SaveMeshAsset(mesh);
        }

        private void SaveMeshAsset(Mesh mesh)
        {
            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(savePath);
            if (existingMesh != null)
            {
                existingMesh.Clear();
                EditorUtility.CopySerialized(mesh, existingMesh);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=cyan>[Ribbon Generator]</color> Overwrote existing Double-Sided mesh at {savePath}");
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, savePath);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=cyan>[Ribbon Generator]</color> Created new Double-Sided mesh at {savePath}");
            }

            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Mesh>(savePath));
        }
    }
}