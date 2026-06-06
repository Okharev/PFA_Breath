using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Editor
{
    public class VertexPainterWindow : EditorWindow
    {
        private BrushSettings brush;

        // Mass Replace specific UI variables
        private Color32 massReplaceTargetColor = Color.white;
        private float massReplaceTolerance = 0.1f;
        private VertexPainterEngine painterEngine;
        private MeshCollider proxyCollider;
        private GameObject currentHoveredObject; // NEW: Cache to prevent recursive rendering

        private GameObject proxyObject;

        private bool showVertexColors = true;
        private bool showVertexPoints = true; // Toggle for vertex dots

        private Material vertexColorPreviewMaterial;

        private float vertexPointSize = 0.02f;
        
        private bool requestSave = false;

        private void OnEnable()
        {
            painterEngine = new VertexPainterEngine();

            brush = new BrushSettings
            {
                radius = 1.0f,
                opacity = 1.0f,
                falloff = 2.0f,
                targetColor = Color.white,
                channelMask = ColorChannel.All,
                mode = PaintMode.Replace
            };

            // Subscribe to the Scene View rendering loop
            SceneView.duringSceneGui += OnSceneGUI;

            Shader previewShader = Shader.Find("Hidden/VertexColorPreview");
            if (previewShader != null)
                vertexColorPreviewMaterial = new Material(previewShader);
            else
                Debug.LogWarning("VertexColorPreview shader not found. Ensure it is created.");

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            // Always clean up Editor event subscriptions and temporary materials
            SceneView.duringSceneGui -= OnSceneGUI;
            if (vertexColorPreviewMaterial != null) DestroyImmediate(vertexColorPreviewMaterial);

            Undo.undoRedoPerformed -= OnUndoRedo;
            if (proxyObject != null) DestroyImmediate(proxyObject);
        }

        private void OnGUI()
        {
            GUILayout.Label("Brush Settings", EditorStyles.boldLabel);

            brush.radius = EditorGUILayout.Slider("Radius", brush.radius, 0.1f, 10f);
            brush.opacity = EditorGUILayout.Slider("Opacity", brush.opacity, 0f, 1f);
            brush.falloff = EditorGUILayout.Slider("Falloff", brush.falloff, 0.1f, 5f);
            brush.targetColor = EditorGUILayout.ColorField("Brush Color", brush.targetColor);
            brush.channelMask = (ColorChannel)EditorGUILayout.EnumFlagsField("Channel Mask", brush.channelMask);
            brush.mode = (PaintMode)EditorGUILayout.EnumPopup("Blend Mode", brush.mode);

            EditorGUILayout.Space();
            GUILayout.Label("View Settings", EditorStyles.boldLabel);
            showVertexColors = EditorGUILayout.Toggle("Preview Vertex Colors", showVertexColors);

            showVertexPoints = EditorGUILayout.Toggle("Show Vertex Points", showVertexPoints);
            if (showVertexPoints)
            {
                EditorGUI.indentLevel++;
                vertexPointSize = EditorGUILayout.Slider("Point Size", vertexPointSize, 0.005f, 0.1f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            GUILayout.Label("Mass Replace (Selected Object)", EditorStyles.boldLabel);
            massReplaceTargetColor = EditorGUILayout.ColorField("Target Color", massReplaceTargetColor);
            massReplaceTolerance = EditorGUILayout.Slider("Tolerance", massReplaceTolerance, 0f, 1f);

            if (GUILayout.Button("Mass Replace Color"))
            {
                if (Selection.activeGameObject != null)
                {
                    MeshFilter mf = Selection.activeGameObject.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        // Defer the mesh modification
                        EditorApplication.delayCall += () => 
                        {
                            Undo.RecordObject(mf.sharedMesh, "Mass Replace Vertex Colors");
                            painterEngine.MassReplaceColor(mf.sharedMesh, massReplaceTargetColor, brush.targetColor, massReplaceTolerance);
                            SceneView.RepaintAll();
                        };
                    }
                }
            }

            EditorGUILayout.Space();
            GUILayout.Label("File Management", EditorStyles.boldLabel);
        
            if (GUILayout.Button("Save Painted Mesh as Asset", GUILayout.Height(30)))
            {
                // Just queue the request. Do not execute it here!
                requestSave = true; 
            }
        }
        
        // NEW: This runs outside the OnGUI drawing loop
        private void Update()
        {
            if (requestSave)
            {
                requestSave = false; // Reset the flag
                SavePaintedMesh();   // Safely execute the OS dialog
            }
        }

        [MenuItem("Tools/Vertex Painter")]
        public static void ShowWindow()
        {
            VertexPainterWindow window = GetWindow<VertexPainterWindow>("Vertex Painter");
            window.Show();
        }

        private void OnUndoRedo()
        {
            // Defer the GPU upload until the frame has finished drawing
            EditorApplication.delayCall += () =>
            {
                if (Selection.activeGameObject != null)
                {
                    MeshFilter mf = Selection.activeGameObject.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        mf.sharedMesh.UploadMeshData(false); 
                    }
                }
                SceneView.RepaintAll(); 
            };
        }

        private void SavePaintedMesh()
        {
            if (Selection.activeGameObject == null)
            {
                Debug.LogWarning("Please select the painted object in the scene first.");
                return;
            }

            MeshFilter mf = Selection.activeGameObject.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogWarning("Selected object does not have a MeshFilter or a valid Mesh.");
                return;
            }

            Mesh currentMesh = mf.sharedMesh;

            // Notice how the OS dialog is now safely executing OUTSIDE of the OnGUI loop
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Painted Mesh",
                currentMesh.name + "_Painted",
                "asset",
                "Choose a location to save your mesh"
            );

            // If the user clicked cancel or closed the window
            if (string.IsNullOrEmpty(path)) return; 

            // Clone the mesh memory to sever ties with the FBX/OBJ source
            Mesh meshToSave = Instantiate(currentMesh);
            meshToSave.name = currentMesh.name + "_Painted";

            // Write the cloned mesh to the disk
            AssetDatabase.CreateAsset(meshToSave, path);
            AssetDatabase.SaveAssets();

            // Swap the active mesh on the GameObject to the newly saved asset
            Undo.RecordObject(mf, "Assign Saved Mesh");
            mf.sharedMesh = meshToSave;

            // Destroy our Ghost Proxy so it cleanly rebuilds with the new mesh on the next hover
            if (proxyObject != null)
            {
                DestroyImmediate(proxyObject);
            }

            // Force the scene to update and show the new permanent asset
            SceneView.RepaintAll();
            
            Debug.Log($"<b>[Vertex Painter]</b> Successfully saved mesh to: <color=#00FF00>{path}</color>");
        }

private void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;
        int controlID = GUIUtility.GetControlID(FocusType.Passive);

        // 1. LAYOUT EVENT: Claim mouse focus
        if (e.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(controlID);
        }

        // 2. MOUSE EVENT: Only pick the object when the mouse moves to PREVENT recursive rendering
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
        {
            currentHoveredObject = HandleUtility.PickGameObject(e.mousePosition, false);
            sceneView.Repaint(); // Only force a UI redraw when the mouse actually moves
        }

        // 3. EXECUTE LOGIC based on the cached hovered object
        if (currentHoveredObject != null && currentHoveredObject.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
        {
            if (proxyObject == null)
            {
                proxyObject = new GameObject("VertexPainterProxy");
                proxyObject.hideFlags = HideFlags.HideAndDontSave; 
                proxyCollider = proxyObject.AddComponent<MeshCollider>();
            }

            proxyObject.transform.SetPositionAndRotation(currentHoveredObject.transform.position, currentHoveredObject.transform.rotation);
            proxyObject.transform.localScale = currentHoveredObject.transform.lossyScale;

            if (proxyCollider.sharedMesh != mf.sharedMesh)
                proxyCollider.sharedMesh = mf.sharedMesh;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (proxyCollider.Raycast(ray, out RaycastHit hit, 1000f))
            {
                // -- VISUALS ONLY (Gated perfectly to Repaint) --
                if (e.type == EventType.Repaint)
                {
                    DrawBrushGizmo(hit.point, hit.normal);
                    VisualizeVertexColors(mf);
                    if (showVertexPoints) DrawVertexPoints(mf, hit.point);
                }

                // -- MODIFICATIONS ONLY (Gated perfectly to Mouse Drag/Click) --
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                {
                    Undo.RecordObject(mf.sharedMesh, "Paint Vertices");
                    painterEngine.ApplyBrush(mf, hit.point, brush);
                    e.Use(); // Consume event
                }
            }
        }
    }
        
        private void DrawVertexPoints(MeshFilter meshFilter, Vector3 hitPoint)
        {
            Mesh mesh = meshFilter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals; // Fetch normals for backface culling
            Color32[] colors = mesh.colors32;
            Transform transform = meshFilter.transform;

            bool hasColors = colors.Length == vertices.Length;
            bool hasNormals = normals.Length == vertices.Length;

            float visibleRadius = brush.radius * 1.2f;

            // IMPORTANT: Save the current Z-Test state so we don't break other Editor tools
            CompareFunction originalZTest = Handles.zTest;

            // Force the dots to respect 3D depth (fixes the X-Ray issue)
            Handles.zTest = CompareFunction.LessEqual;

            // Get the current Scene View camera position for backface culling
            Vector3 cameraPos = SceneView.currentDrawingSceneView.camera.transform.position;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = transform.TransformPoint(vertices[i]);

                if (Vector3.Distance(worldPos, hitPoint) <= visibleRadius)
                {
                    // BACKFACE CULLING: Don't draw vertices that are pointing away from the camera
                    if (hasNormals)
                    {
                        Vector3 worldNormal = transform.TransformDirection(normals[i]);
                        Vector3 viewDir = worldPos - cameraPos;

                        // If the dot product is positive, the normal is facing away from us
                        if (Vector3.Dot(worldNormal, viewDir) > 0) continue;
                    }

                    // Calculate scale using your custom slider value
                    float currentHandleSize = HandleUtility.GetHandleSize(worldPos) * vertexPointSize;

                    if (hasColors)
                        Handles.color = new Color32(colors[i].r, colors[i].g, colors[i].b, 255);
                    else
                        Handles.color = Color.white;

                    Handles.DotHandleCap(
                        0,
                        worldPos,
                        Quaternion.identity,
                        currentHandleSize,
                        EventType.Repaint
                    );
                }
            }

            // Restore the original Z-Test state
            Handles.zTest = originalZTest;
        }

        private void DrawBrushGizmo(Vector3 hitPoint, Vector3 normal)
        {
            Handles.color = new Color(brush.targetColor.r / 255f, brush.targetColor.g / 255f,
                brush.targetColor.b / 255f, 1f);
            Handles.DrawWireDisc(hitPoint, normal, brush.radius);

            Handles.color = new Color(1, 1, 1, 0.3f);
            Handles.DrawWireDisc(hitPoint, normal, brush.radius * (1f / brush.falloff));
            Handles.DrawLine(hitPoint, hitPoint + normal * (brush.radius * 0.5f));
        }

        private void VisualizeVertexColors(MeshFilter meshFilter)
        {
            if (!showVertexColors || vertexColorPreviewMaterial == null) return;

            Graphics.DrawMesh(
                meshFilter.sharedMesh,
                meshFilter.transform.localToWorldMatrix,
                vertexColorPreviewMaterial,
                0,
                SceneView.currentDrawingSceneView.camera
            );
        }
    }
}