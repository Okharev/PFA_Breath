using UnityEditor;
using UnityEngine;

namespace TechArtPlayground.Editor
{
    public class VolumetricNoiseGenerator : EditorWindow
    {
        private int resolution = 64;
        private float brightness = 1.2f;
        private float contrast = 2.0f;

        [MenuItem("Tools/Graphics/Generate 3D Fog Noise")]
        public static void ShowWindow()
        {
            GetWindow<VolumetricNoiseGenerator>("3D Noise Generator");
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("Fractal Worley Noise (Tileable)", EditorStyles.boldLabel);
            GUILayout.Space(5);
        
            // 64x64x64 is the AAA standard for volumetric noise. 
            // Going to 128x128x128 increases file size by 8x for minimal visual gain.
            resolution = EditorGUILayout.IntSlider("Resolution", resolution, 32, 128);
            brightness = EditorGUILayout.Slider("Brightness", brightness, 0.5f, 3.0f);
            contrast = EditorGUILayout.Slider("Contrast", contrast, 0.5f, 4.0f);

            GUILayout.Space(20);

            if (GUILayout.Button("Bake 3D Texture", GUILayout.Height(30)))
            {
                GenerateAndSaveTexture();
            }
        }

        private void GenerateAndSaveTexture()
        {
            EditorUtility.DisplayProgressBar("Baking 3D Noise", "Calculating distances...", 0.0f);

            // We use R8 (Single channel 8-bit) to drastically reduce VRAM footprint.
            // We only need the red channel to multiply our fog density.
            Texture3D tex = new Texture3D(resolution, resolution, resolution, TextureFormat.R8, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color32[] colors = new Color32[resolution * resolution * resolution];

            // Generate 3 octaves of random points for fractal detail
            Vector3[] oct1 = GeneratePoints(12);  // Base shapes
            Vector3[] oct2 = GeneratePoints(40);  // Mid details
            Vector3[] oct3 = GeneratePoints(150); // Fine wisps

            for (int z = 0; z < resolution; z++)
            {
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        Vector3 pos = new Vector3((float)x / resolution, (float)y / resolution, (float)z / resolution);

                        // Sample inverted distances (creates cloud shapes)
                        float v1 = 1.0f - GetMinDistance(pos, oct1);
                        float v2 = 1.0f - GetMinDistance(pos, oct2);
                        float v3 = 1.0f - GetMinDistance(pos, oct3);

                        // Fractal composite
                        float noise = (v1 * 1.0f) + (v2 * 0.5f) + (v3 * 0.25f);
                        noise /= 1.75f; // Normalize back to 0-1 range

                        // Apply artist tuning
                        noise = Mathf.Pow(noise * brightness, contrast);
                        noise = Mathf.Clamp01(noise);

                        byte col = (byte)(noise * 255);
                    
                        // 1D array index mapping for 3D texture
                        int index = x + (y * resolution) + (z * resolution * resolution);
                        colors[index] = new Color32(col, col, col, col);
                    }
                }
            }

            tex.SetPixels32(colors);
            tex.Apply();

            string path = "Assets/VolumetricCloudNoise3D.asset";
            AssetDatabase.CreateAsset(tex, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();
            Debug.Log($"[Volumetric Fog] Successfully baked 3D Noise Texture to: {path}");
        
            // Highlight the new asset in the Project window
            EditorGUIUtility.PingObject(tex);
        }

        private Vector3[] GeneratePoints(int count)
        {
            Vector3[] pts = new Vector3[count];
            for (int i = 0; i < count; i++) 
            {
                pts[i] = new Vector3(Random.value, Random.value, Random.value);
            }
            return pts;
        }

        private float GetMinDistance(Vector3 pos, Vector3[] points)
        {
            float minDist = 100f;
            foreach (var p in points)
            {
                // Modular arithmetic to ensure perfectly seamless tiling across the 3D grid
                float dx = Mathf.Abs(pos.x - p.x); dx = Mathf.Min(dx, 1f - dx);
                float dy = Mathf.Abs(pos.y - p.y); dy = Mathf.Min(dy, 1f - dy);
                float dz = Mathf.Abs(pos.z - p.z); dz = Mathf.Min(dz, 1f - dz);
            
                float dist = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                if (dist < minDist) minDist = dist;
            }
            return minDist;
        }
    }
}