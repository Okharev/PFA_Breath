using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Required for InspectorElement

namespace Dialogues.Editor
{
    public class DialogueEditorWindow : EditorWindow
    {
        private Conversation _currentConversation;
        private DialogueGraphView _graphView;
        private ScrollView _sidePanel;

        private void OnEnable()
        {
            ConstructWindowLayout();
            GenerateToolbar();
        }

        private void OnDisable()
        {
            rootVisualElement.Clear();
        }

        [MenuItem("Tools/Dialogue Graph Editor")]
        public static void OpenDialogueGraphWindow()
        {
            DialogueEditorWindow window = GetWindow<DialogueEditorWindow>();
            window.titleContent = new GUIContent("Dialogue Editor");
        }

        private void ConstructWindowLayout()
        {
            // 1. Create a resizable split view
            TwoPaneSplitView splitView = new(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1; // Ensure it fills the window

            // 2. Create the Graph View (Left Side)
            _graphView = new DialogueGraphView(this) { name = "Dialogue Graph" };
            _graphView.OnNodeSelected = UpdateSidePanel; // Bind the selection event

            // 3. Create the Side Panel (Right Side)
            _sidePanel = new ScrollView();
            _sidePanel.style.paddingTop = 10;
            _sidePanel.style.paddingLeft = 10;
            _sidePanel.style.paddingRight = 10;
            _sidePanel.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

            // 4. Add panes to split view
            splitView.Add(_graphView);
            splitView.Add(_sidePanel);
            rootVisualElement.Add(splitView);
        }

        private void UpdateSidePanel(DialogueNodeView nodeView)
        {
            _sidePanel.Clear();

            if (nodeView == null || nodeView.NodeData == null) return;

            SerializedObject serializedObject = new(nodeView.NodeData);
            InspectorElement inspector = new(serializedObject);

            _sidePanel.Add(inspector);
            _sidePanel.Bind(serializedObject);

            // 1. Track Choices (from previous step)
            SerializedProperty choicesProperty = serializedObject.FindProperty("choices");
            if (choicesProperty != null)
                inspector.TrackPropertyValue(choicesProperty, prop => nodeView.SyncChoicePorts(_graphView));

            // --- Track Speaker Changes (Updates Color/Title) ---
            SerializedProperty speakerProperty = serializedObject.FindProperty("speaker");
            if (speakerProperty != null)
                inspector.TrackPropertyValue(speakerProperty, prop => nodeView.RefreshVisuals());

            // --- Track Text Changes (Updates Preview) ---
            SerializedProperty textProperty = serializedObject.FindProperty("text");
            if (textProperty != null) inspector.TrackPropertyValue(textProperty, prop => nodeView.RefreshVisuals());
        }

        private void GenerateToolbar()
        {
            Toolbar toolbar = new();

            // --- NEW: Add an Object Field to select the Conversation asset ---
            ObjectField conversationField = new("Active Conversation")
            {
                objectType = typeof(Conversation),
                allowSceneObjects = false,
                value = _currentConversation
            };

            conversationField.RegisterValueChangedCallback(evt => 
            {
                _currentConversation = evt.newValue as Conversation;
                LoadData(); // <--- Auto-load when a new conversation is selected!
            });

            toolbar.Add(conversationField);

            // Update the Save Button
            Button saveButton = new(() => SaveData()) { text = "Save Data" };
            toolbar.Add(saveButton);

            rootVisualElement.Insert(0, toolbar);
        }

        private void SaveData()
        {
            if (_currentConversation == null) return;

            List<DialogueNodeView> nodeViews = _graphView.nodes.ToList().Cast<DialogueNodeView>().ToList();

            foreach (DialogueNodeView nodeView in nodeViews)
            {
                // --- NEW: Record the visual position to the ScriptableObject ---
                nodeView.NodeData.position = nodeView.GetPosition().position;

                if (!AssetDatabase.Contains(nodeView.NodeData))
                    AssetDatabase.AddObjectToAsset(nodeView.NodeData, _currentConversation);
            }

            // 3. Automatically determine the Starting Node
            // The starting node is usually the one that has NO wires connected to its Input port
            DialogueNodeView rootNode = nodeViews.FirstOrDefault(n => !n.InputPort.connected);
            if (rootNode != null)
            {
                _currentConversation.startingNode = rootNode.NodeData;
                EditorUtility.SetDirty(_currentConversation); // Mark the conversation as changed
            }

            // 4. Force Unity to write the data to disk
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"<color=green>Successfully saved {nodeViews.Count} nodes to {_currentConversation.name}!</color>");
        }



        private void LoadData()
        {
            if (_currentConversation == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign an Active Conversation to load.", "OK");
                return;
            }

            ClearGraph();

            // 1. Fetch all DialogueNode sub-assets hidden inside the active Conversation asset
            string assetPath = AssetDatabase.GetAssetPath(_currentConversation);
            List<DialogueNode> savedNodes = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<DialogueNode>().ToList();

            if (savedNodes.Count == 0) return; // Nothing to load

            // We use a dictionary to easily map our C# Data back to the newly spawned Visual Nodes
            Dictionary<DialogueNode, DialogueNodeView> nodeDictionary = new();

            // 2. SPAWN NODES: Create the visual elements and place them at their saved positions
            foreach (DialogueNode nodeData in savedNodes)
            {
                DialogueNodeView nodeView = new(nodeData);

                // Restore the exact position the designer left it in
                nodeView.SetPosition(new Rect(nodeData.position, Vector2.zero));
                nodeView.OnSelectedCallback = UpdateSidePanel;

                _graphView.AddElement(nodeView);
                nodeDictionary.Add(nodeData, nodeView);
            }

            // 3. RECONNECT WIRES: Loop through again to draw the edges
            foreach (DialogueNodeView nodeView in nodeDictionary.Values)
            {
                // A. Reconnect the Default Next Node
                if (nodeView.NodeData.nextNode != null && nodeDictionary.TryGetValue(nodeView.NodeData.nextNode,
                        out DialogueNodeView targetNodeView))
                    LinkPorts(nodeView.DefaultOutputPort, targetNodeView.InputPort);

                // B. Reconnect Choices
                for (int i = 0; i < nodeView.NodeData.choices.Count; i++)
                {
                    DialogueChoice choice = nodeView.NodeData.choices[i];

                    // If the choice has a destination, and we successfully loaded that destination
                    if (choice.nextNode != null &&
                        nodeDictionary.TryGetValue(choice.nextNode, out DialogueNodeView choiceTargetView))
                        // Ensure the visual port exists (it should, as the NodeView constructor calls SyncChoicePorts)
                        if (i < nodeView.ChoicePorts.Count)
                            LinkPorts(nodeView.ChoicePorts[i], choiceTargetView.InputPort);
                }
            }
        }

// Helper: Safely deletes all existing visual elements
        private void ClearGraph()
        {
            UpdateSidePanel(null); // Clear the inspector

            // Delete all wires and nodes currently on the screen
            _graphView.DeleteElements(_graphView.edges.ToList());
            _graphView.DeleteElements(_graphView.nodes.ToList());
        }

// Helper: Physically draws a wire between two ports
        private void LinkPorts(Port outputPort, Port inputPort)
        {
            Edge edge = new()
            {
                output = outputPort,
                input = inputPort
            };

            // Connect the data logic
            edge?.input.Connect(edge);
            edge?.output.Connect(edge);

            // Add the visual wire to the graph
            _graphView.AddElement(edge);
        }
    }
}