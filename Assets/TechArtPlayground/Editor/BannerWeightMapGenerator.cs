using UnityEngine;
using UnityEditor;
using System.IO;

namespace TechArtPlayground.EditorTools
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
        
        [Tooltip("1.0 = Rigid, 0.0 = Flexible")]
        private float _topStiffness = 1.0f;
        [Tooltip("1.0 = Rigid, 0.0 = Flexible")]
        private float _bottomStiffness = 0.0f;

        [Tooltip("Percentage of the cloth (from the top down) that skips self-collision. 0.5 disables collision for the top half.")]
        private float _selfCollideCutoff = 0.5f;
        
        // Creates the menu item and opens the window
        [MenuItem("Tools/TechArt/Banner Weight Map Generator")]
        public static void ShowWindow()
        {
            // Opens a custom dockable Unity window
            GetWindow<BannerWeightMapGenerator>("Weight Map Gen");
        }

        // Draws the UI inside the window
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
            _topStiffness = EditorGUILayout.Slider("Top Stiffness", _topStiffness, 0f, 1f);
            _bottomStiffness = EditorGUILayout.Slider("Bottom Stiffness", _bottomStiffness, 0f, 1f);

            // Add this right below your Stiffness sliders
            EditorGUILayout.Space();
            GUILayout.Label("Optimization (Blue Channel)", EditorStyles.miniLabel);
            _selfCollideCutoff = EditorGUILayout.Slider(new GUIContent("Disable Collision Top %", "Saves GPU cycles by turning off self-collision for the top taut parts of the cloth."), _selfCollideCutoff, 0f, 1f);
            
            EditorGUILayout.Space();

            GUILayout.Label("Export Settings", EditorStyles.boldLabel);
            _folderPath = EditorGUILayout.TextField("Save Folder", _folderPath);
            _fileName = EditorGUILayout.TextField("File Name", _fileName);

            EditorGUILayout.Space();

            // The big generate button
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f); // Make it pop!
            if (GUILayout.Button("Generate Weight Map", GUILayout.Height(40)))
            {
                GenerateTexture();
            }
            GUI.backgroundColor = Color.white;
        }

        private void GenerateTexture()
        {
            // Sanitize inputs
            _width = Mathf.Max(2, _width);
            _height = Mathf.Max(2, _height);
            if (string.IsNullOrEmpty(_fileName)) _fileName = "DefaultWeightMap";

            Texture2D tex = new Texture2D(_width, _height, TextureFormat.RGBA32, false);

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    float v = (float)y / (_height - 1); // 0.0 at bottom, 1.0 at top

                    // --- RED (Mass) ---
                    bool isPinned = y >= (_height - _pinnedTopRows);
                    float r = isPinned ? 0.0f : 1.0f;

                    // --- GREEN (Stiffness) ---
                    float g = Mathf.Lerp(_bottomStiffness, _topStiffness, v);

                    // --- BLUE (Self-Collision Mask) ---
                    // If V (height) is greater than our cutoff threshold, disable collision (0.0).
                    // Example: If cutoff is 0.5, the top 50% of the cloth gets 0.0 (No Collision).
                    float collisionThreshold = 1.0f - _selfCollideCutoff;
                    float b = (v >= collisionThreshold) ? 0.0f : 1.0f;

                    // Apply all three channels
                    tex.SetPixel(x, y, new Color(r, g, b, 1f));
                }
            }
            tex.Apply();

            // Ensure directory exists
            if (!Directory.Exists(_folderPath))
            {
                Directory.CreateDirectory(_folderPath);
            }

            // Format file path
            string fullPath = $"{_folderPath}/{_fileName}.png";
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(fullPath, bytes);

            AssetDatabase.Refresh();

            // Auto-configure Unity Import Settings
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
            
            // Optionally "ping" the asset in the project window so you don't have to look for it
            Object pingObj = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            if (pingObj != null) EditorGUIUtility.PingObject(pingObj);
        }
    }
}