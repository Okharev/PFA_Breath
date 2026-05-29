using UnityEditor;
using UnityEngine;

namespace TechArtPlayground.Editor
{
    public class BannerMeshGenerator : EditorWindow
    {
        // Paramètres de taille
        private float _width = 2.0f;
        private float _height = 4.0f;
        
        // Paramètres de résolution (plus il y a de segments, plus la bannière est souple)
        private int _segmentsX = 10;
        private int _segmentsY = 20;

        // Ancrage (pour les Vertex Colors)
        public enum AnchorMode { Top, Left, Right, Bottom, PinnedCorners }
        private AnchorMode _anchorMode = AnchorMode.Top;

        // Sauvegarde
        private string _saveName = "NewBannerMesh";

        // Ajoute un menu dans Unity pour ouvrir la fenêtre
        [MenuItem("TechArt/Banner Mesh Generator")]
        public static void ShowWindow()
        {
            // Ouvre ou met au premier plan la fenêtre
            GetWindow<BannerMeshGenerator>("Banner Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Paramètres de la Bannière", EditorStyles.boldLabel);

            _width = EditorGUILayout.FloatField("Largeur (Width)", _width);
            _height = EditorGUILayout.FloatField("Hauteur (Height)", _height);
            
            _segmentsX = EditorGUILayout.IntSlider("Segments X", _segmentsX, 1, 100);
            _segmentsY = EditorGUILayout.IntSlider("Segments Y", _segmentsY, 1, 100);

            GUILayout.Space(10);
            GUILayout.Label("Paramètres Physiques (Vertex Colors)", EditorStyles.boldLabel);
            _anchorMode = (AnchorMode)EditorGUILayout.EnumPopup("Point d'Ancrage", _anchorMode);
            EditorGUILayout.HelpBox("Le point d'ancrage définit quelle partie de la bannière est fixée (couleur rouge = 0) et quelle partie bouge avec le vent (couleur rouge = 1).", MessageType.Info);

            GUILayout.Space(10);
            GUILayout.Label("Sauvegarde", EditorStyles.boldLabel);
            _saveName = EditorGUILayout.TextField("Nom du fichier", _saveName);

            GUILayout.Space(20);
            
            // Bouton de génération
            if (GUILayout.Button("Générer et Sauvegarder", GUILayout.Height(40)))
            {
                GenerateAndSaveMesh();
            }
        }

        private void GenerateAndSaveMesh()
        {
            // 1. Calcul du nombre de sommets
            int vertexCount = (_segmentsX + 1) * (_segmentsY + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Color[] colors = new Color[vertexCount];

            // 2. Génération des Sommets (Vertices), UVs, et Couleurs
            for (int y = 0; y <= _segmentsY; y++)
            {
                for (int x = 0; x <= _segmentsX; x++)
                {
                    int index = y * (_segmentsX + 1) + x;
                    
                    // Ratio de 0 à 1
                    float u = (float)x / _segmentsX;
                    float v = (float)y / _segmentsY;

                    // Position (Le pivot est au centre-haut : X est centré, Y descend)
                    vertices[index] = new Vector3((u - 0.5f) * _width, -v * _height, 0);
                    
                    normals[index] = Vector3.back; // Face à la caméra par défaut
                    uvs[index] = new Vector2(u, 1.0f - v); // UV standard

                    // Calcul de l'ancre (Vertex Color)
                    float weight = 1.0f;
                    switch (_anchorMode)
                    {
                        case AnchorMode.Top: weight = v; break; // v=0 en haut (fixé), v=1 en bas (bouge)
                        case AnchorMode.Bottom: weight = 1.0f - v; break;
                        case AnchorMode.Left: weight = u; break;
                        case AnchorMode.Right: weight = 1.0f - u; break;
                        case AnchorMode.PinnedCorners:
                            // Fixé uniquement aux deux coins supérieurs
                            weight = (y == 0 && (x == 0 || x == _segmentsX)) ? 0.0f : 1.0f;
                            break;
                    }

                    // On stocke le poids d'ancre dans le canal Rouge (r) pour le Shader
                    colors[index] = new Color(weight, weight, weight, 1.0f);
                }
            }

            // 3. Génération des Triangles
            int[] triangles = new int[_segmentsX * _segmentsY * 6];
            int ti = 0;
            for (int y = 0; y < _segmentsY; y++)
            {
                for (int x = 0; x < _segmentsX; x++)
                {
                    int i0 = y * (_segmentsX + 1) + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + (_segmentsX + 1);
                    int i3 = i2 + 1;

                    // Triangle 1
                    triangles[ti++] = i0;
                    triangles[ti++] = i1;
                    triangles[ti++] = i2;

                    // Triangle 2
                    triangles[ti++] = i1;
                    triangles[ti++] = i3;
                    triangles[ti++] = i2;
                }
            }

            // 4. Création de l'objet Mesh
            Mesh mesh = new Mesh();
            mesh.name = _saveName;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = triangles;

            // Recalcule les tangentes (très important pour les calculs de vent de ton shader)
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            // 5. Sauvegarde dans les Assets
            string folderPath = "Assets/Models/Banners";
            if (!AssetDatabase.IsValidFolder("Assets/Models")) AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder(folderPath)) AssetDatabase.CreateFolder("Assets/Models", "Banners");

            string fullPath = $"{folderPath}/{_saveName}.asset";
            
            // Assure-toi de ne pas écraser accidentellement en ajoutant un numéro si besoin,
            // ou écrase si tu mets à jour le même maillage.
            AssetDatabase.CreateAsset(mesh, fullPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=green>Succès :</color> Maillage de bannière généré et sauvegardé sous : {fullPath}");
            
            // Sélectionne automatiquement le nouveau fichier dans la fenêtre Projet
            Selection.activeObject = mesh;
        }
    }
}