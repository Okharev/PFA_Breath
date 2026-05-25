using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TechArtPlayground.Editor
{
    public class FishMeshGenerator : EditorWindow
    {
        [Header("Dimensions du poisson")]
        public float fishLength = 1.0f;
        public float fishWidth = 0.2f;
        public float fishHeight = 0.4f;
    
        [Header("Animation (Subdivisions)")]
        [Tooltip("Plus il y a de segments, plus l'ondulation de la nage sera fluide.")]
        public int bodySegments = 6;
    
        [Header("Nageoire caudale (Queue)")]
        public float tailLength = 0.3f;
        public float tailHeight = 0.5f;

        [MenuItem("Tools/Boids Fish Generator")]
        public static void ShowWindow()
        {
            GetWindow<FishMeshGenerator>("Fish Generator");
        }

        void OnGUI()
        {
            GUILayout.Label("Générateur de Poisson Low-Poly", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            fishLength = EditorGUILayout.FloatField("Longueur (Z)", fishLength);
            fishWidth = EditorGUILayout.FloatField("Largeur (X)", fishWidth);
            fishHeight = EditorGUILayout.FloatField("Hauteur (Y)", fishHeight);
        
            EditorGUILayout.Space();
            bodySegments = EditorGUILayout.IntSlider("Segments du corps", bodySegments, 3, 15);
        
            EditorGUILayout.Space();
            tailLength = EditorGUILayout.FloatField("Longueur de la queue", tailLength);
            tailHeight = EditorGUILayout.FloatField("Hauteur de la queue", tailHeight);

            EditorGUILayout.Space();
            if (GUILayout.Button("Générer et Sauvegarder le Modèle", GUILayout.Height(40)))
            {
                GenerateMesh();
            }
        }

        void GenerateMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "BoidFish";

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            int numRings = bodySegments - 1;
        
            // 1. Le Nez (Avant)
            vertices.Add(new Vector3(0, 0, fishLength / 2f));
            uvs.Add(new Vector2(0.5f, 1f));

            // 2. Les anneaux du corps (Profil en diamant)
            for (int i = 0; i < numRings; i++)
            {
                float t = (float)(i + 1) / bodySegments;
                float z = Mathf.Lerp(fishLength / 2f, -fishLength / 2f, t);
            
                // Courbe mathématique pour donner une forme rebondie au poisson
                float profile = Mathf.Sin(t * Mathf.PI); 

                float w = (fishWidth / 2f) * profile;
                float h = (fishHeight / 2f) * profile;

                vertices.Add(new Vector3(0, h, z));  // Haut
                vertices.Add(new Vector3(w, 0, z));  // Droite
                vertices.Add(new Vector3(0, -h, z)); // Bas
                vertices.Add(new Vector3(-w, 0, z)); // Gauche

                float v = 1f - t;
                uvs.Add(new Vector2(0.5f, v));
                uvs.Add(new Vector2(1f, v));
                uvs.Add(new Vector2(0.5f, v));
                uvs.Add(new Vector2(0f, v));
            }

            // 3. Base de la queue
            int tailBaseIndex = vertices.Count;
            vertices.Add(new Vector3(0, 0, -fishLength / 2f));
            uvs.Add(new Vector2(0.5f, 0f));

            // 4. Nageoire
            int finTopIndex = vertices.Count;
            vertices.Add(new Vector3(0, tailHeight / 2f, -fishLength / 2f - tailLength));
            uvs.Add(new Vector2(1f, 0f));
        
            int finBotIndex = vertices.Count;
            vertices.Add(new Vector3(0, -tailHeight / 2f, -fishLength / 2f - tailLength));
            uvs.Add(new Vector2(0f, 0f));

            // --- TRIANGLES ---

            // Nez vers le premier anneau
            triangles.AddRange(new int[] { 0, 2, 1,   0, 3, 2,   0, 4, 3,   0, 1, 4 });

            // Corps
            for (int r = 0; r < numRings - 1; r++)
            {
                int c = 1 + r * 4;
                int n = 1 + (r + 1) * 4;

                triangles.AddRange(new int[] { c, n+1, n,       c, c+1, n+1 }); // Top-Right
                triangles.AddRange(new int[] { c+1, n+2, n+1,   c+1, c+2, n+2 }); // Right-Bot
                triangles.AddRange(new int[] { c+2, n+3, n+2,   c+2, c+3, n+3 }); // Bot-Left
                triangles.AddRange(new int[] { c+3, n, n+3,     c+3, c, n });     // Left-Top
            }

            // Dernier anneau vers base de la queue
            int lastRing = 1 + (numRings - 1) * 4;
            triangles.AddRange(new int[] { tailBaseIndex, lastRing, lastRing+1 });
            triangles.AddRange(new int[] { tailBaseIndex, lastRing+1, lastRing+2 });
            triangles.AddRange(new int[] { tailBaseIndex, lastRing+2, lastRing+3 });
            triangles.AddRange(new int[] { tailBaseIndex, lastRing+3, lastRing });

            // Nageoire (Double face)
            triangles.AddRange(new int[] { tailBaseIndex, finTopIndex, finBotIndex }); // Face Droite
            triangles.AddRange(new int[] { tailBaseIndex, finBotIndex, finTopIndex }); // Face Gauche

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();

            // Sauvegarder dans les Assets
            AssetDatabase.CreateAsset(mesh, "Assets/BoidFish_Mesh.asset");
            AssetDatabase.SaveAssets();

            Debug.Log("🐟 Modèle de poisson généré avec succès dans le dossier Assets !");
        }
    }
}