using System.IO;
using UnityEditor;
using UnityEngine;

namespace TechArtPlayground.Editor
{
    public class BannerWeightMapGenerator : EditorWindow
    {
        // --- Customization Variables ---
        private int _width = 64;
        private int _height = 64;
        private string _folderPath = "Assets/TechArtPlayground/Textures";
        private string _fileName = "BannerWeightMap";

        [Header("Physics Settings")]
        private int _pinnedTopRows = 1; // How many rows of vertices are frozen
        
        // FIX 4: Updated Defaults and Tooltips to match exactly what happens under the hood
        [Tooltip("1.0 = Rigid (0 stretch), 0.0 = Flexible (Max Stretch)")]
        private float _topStiffness = 0.8f;
        [Tooltip("1.0 = Rigid (0 stretch), 0.0 = Flexible (Max Stretch)")]
        private float _bottomStiffness = 0.0f;

        [Tooltip("Percentage of the cloth (from the top down) that skips self-collision. 0.5 disables collision for the top half.")]
        private float _selfCollideCutoff = 0.5f;
        
        [MenuItem("Tools/TechArt/Banner Weight Map Generator")]
        public static void ShowWindow()
        {
            GetWindow<BannerWeightMapGenerator>("Weight Map Gen");
        }

        private void OnGUI()
        {
            GUILayout.Label("Texture Resolution", EditorStyles.boldLabel);
            _width = EditorGUILayout.IntSlider("Width", _width, 8, 256);
            _height = EditorGUILayout.IntSlider("Height", _height, 8, 256);

            EditorGUILayout.Space();

            GUILayout.Label("Physics Gradients", EditorStyles.boldLabel);
            _pinnedTopRows = EditorGUILayout.IntSlider(new GUIContent("Pinned Top Rows", "How many rows of pixels at the very top have 0 mass (frozen)."), _pinnedTopRows, 0, 10);
            
            EditorGUILayout.Space();
            GUILayout.Label("Stiffness (Green Channel)", EditorStyles.miniLabel);
            _topStiffness = EditorGUILayout.Slider(new GUIContent("Top Stiffness", "1.0 = Rigid (Cardboard), 0.0 = Flexible (Silk)"), _topStiffness, 0f, 1f);
            _bottomStiffness = EditorGUILayout.Slider(new GUIContent("Bottom Stiffness", "1.0 = Rigid (Cardboard), 0.0 = Flexible (Silk)"), _bottomStiffness, 0f, 1f);

            EditorGUILayout.Space();
            GUILayout.Label("Optimization (Blue Channel)", EditorStyles.miniLabel);
            _selfCollideCutoff = EditorGUILayout.Slider(new GUIContent("Disable Collision Top %", "Saves GPU cycles by turning off self-collision for the top taut parts of the cloth."), _selfCollideCutoff, 0f, 1f);
            
            EditorGUILayout.Space();

            GUILayout.Label("Export Settings", EditorStyles.boldLabel);
            _folderPath = EditorGUILayout.TextField("Save Folder", _folderPath);
            _fileName = EditorGUILayout.TextField("File Name", _fileName);

            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUILayout.Button("Generate Weight Map", GUILayout.Height(40)))
            {
                GenerateTexture();
            }
            GUI.backgroundColor = Color.white;
        }

        private void GenerateTexture()
        {
            _width = Mathf.Max(2, _width);
            _height = Mathf.Max(2, _height);
            if (string.IsNullOrEmpty(_fileName)) _fileName = "DefaultWeightMap";

            Texture2D tex = new Texture2D(_width, _height, TextureFormat.RGBA32, false);

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    float v = (float)y / (_height - 1); 

                    // --- RED (Mass) ---
                    bool isPinned = y >= (_height - _pinnedTopRows);
                    float r = isPinned ? 0.0f : 1.0f;

                    // --- GREEN (Stiffness) ---
                    // Generates 1.0 where you want stiffness, 0.0 where you want flexibility.
                    float g = Mathf.Lerp(_bottomStiffness, _topStiffness, v);

                    // --- BLUE (Self-Collision Mask) ---
                    float collisionThreshold = 1.0f - _selfCollideCutoff;
                    float b = (v >= collisionThreshold) ? 0.0f : 1.0f;

                    tex.SetPixel(x, y, new Color(r, g, b, 1f));
                }
            }
            tex.Apply();

            if (!Directory.Exists(_folderPath))
            {
                Directory.CreateDirectory(_folderPath);
            }

            string fullPath = $"{_folderPath}/{_fileName}.png";
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(fullPath, bytes);

            AssetDatabase.Refresh();

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(fullPath);
            if (importer != null)
            {
                importer.isReadable = true;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                
                importer.SaveAndReimport();
            }

            Debug.Log($"<color=cyan>[TechArt]</color> Successfully generated Weight Map at: {fullPath}");
            
            Object pingObj = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            if (pingObj != null) EditorGUIUtility.PingObject(pingObj);
        }
    }
}