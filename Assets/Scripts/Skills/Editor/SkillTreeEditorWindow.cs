using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.Editor
{
    public class SkillTreeEditorWindow : EditorWindow
    {
        private SkillTreeGraph activeTreeAsset;
        private SkillTreeGraphView graphView;
        private ScrollView inspectorPanel;

        private void OnEnable()
        {
            GenerateToolbar();
            ConstructLayout(); // Replaced ConstructGraphView with our new split layout
        }

        [MenuItem("Window/Custom Tools/Skill Tree Editor")]
        public static void OpenWindow()
        {
            SkillTreeEditorWindow window = GetWindow<SkillTreeEditorWindow>();
            window.titleContent = new GUIContent("Skill Tree Editor");
        }

        private void ConstructLayout()
        {
            // 1. Create the Split View container
            TwoPaneSplitView splitView = new(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1; // Fill the space below the toolbar

            // 2. Create the Left Panel (Graph)
            graphView = new SkillTreeGraphView { name = "Skill Tree Graph" };

            // 3. Create the Right Panel (Inspector)
            inspectorPanel = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            inspectorPanel.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark background
            inspectorPanel.style.paddingLeft = 10;
            inspectorPanel.style.paddingTop = 10;

            // Default text when nothing is selected
            inspectorPanel.Add(new Label("Select a node to edit properties.")
                { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            // Add them to the split view
            splitView.Add(graphView);
            splitView.Add(inspectorPanel);

            rootVisualElement.Add(splitView);
        }

        private void GenerateToolbar()
        {
            Toolbar toolbar = new();

            // 1. The Asset Selector (Drag and drop your SO here)
            ObjectField treeSelector = new("Active Tree")
            {
                objectType = typeof(SkillTreeGraph),
                allowSceneObjects = false,
                value = activeTreeAsset
            };
            treeSelector.RegisterValueChangedCallback(evt => { activeTreeAsset = evt.newValue as SkillTreeGraph; });

            // 2. Buttons
            Button nodeCreateButton = new(() =>
            {
                if (graphView != null)
                {
                    SkillNodeView newNode = graphView.CreateNode("New Skill");
                    newNode.OnNodeSelected += UpdateInspector;
                }
            }) { text = "Create Node" };

            Button saveButton = new(() =>
            {
                if (activeTreeAsset != null) GraphSaveUtility.GetInstance(graphView, activeTreeAsset).SaveGraph();
                else Debug.LogWarning("Please assign an Active Tree asset in the toolbar first!");
            }) { text = "Save Graph" };

            Button loadButton = new(() =>
            {
                if (activeTreeAsset != null)
                {
                    GraphSaveUtility.GetInstance(graphView, activeTreeAsset).LoadGraph();
                    // Re-hook up the inspector click events for all loaded nodes
                    foreach (SkillNodeView node in graphView.nodes.ToList().Cast<SkillNodeView>())
                        node.OnNodeSelected += UpdateInspector;
                    inspectorPanel.Clear(); // Clear inspector on load
                }
                else
                {
                    Debug.LogWarning("Please assign an Active Tree asset in the toolbar first!");
                }
            }) { text = "Load Graph" };

            // Add elements to toolbar
            toolbar.Add(treeSelector);
            toolbar.Add(nodeCreateButton);

            // Add a flexible spacer to push Save/Load buttons to the right side
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            toolbar.Add(saveButton);
            toolbar.Add(loadButton);

            rootVisualElement.Add(toolbar);
        }

        // --- THE INSPECTOR LOGIC ---
        private void UpdateInspector(SkillNodeView selectedNode)
        {
            inspectorPanel.Clear();
            if (selectedNode == null) return;

            // Header
            Label header = new($"Editing: {selectedNode.NodeData.NodeName}")
                { style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 10 } };
            inspectorPanel.Add(header);

            // --- 1. Basic Identity ---
            TextField nameField = new("Node Name") { value = selectedNode.NodeData.NodeName };
            nameField.RegisterValueChangedCallback(evt =>
            {
                selectedNode.NodeData.NodeName = evt.newValue;
                selectedNode.title = evt.newValue;
            });
            inspectorPanel.Add(nameField);

            TextField descField = new("Description") { value = selectedNode.NodeData.Description, multiline = true };
            descField.RegisterValueChangedCallback(evt => selectedNode.NodeData.Description = evt.newValue);
            inspectorPanel.Add(descField);

            // ==========================================
            // --- 2. EXCLUSIVE TYPE & COSTS ---
            // ==========================================
            inspectorPanel.Add(new Label("Node Type & Costs")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 15, marginBottom = 5 } });

            // Containers for exclusive fields so we can hide/show them in bulk
            VisualElement genericCostContainer = new();
            VisualElement emotionCostContainer = new();

            // The Type Dropdown
            EnumField typeField = new("Node Type", selectedNode.NodeData.NodeType);
            typeField.RegisterValueChangedCallback(evt =>
            {
                selectedNode.NodeData.NodeType = (NodeType)evt.newValue;

                // Hide/Show logic
                genericCostContainer.style.display = selectedNode.NodeData.NodeType == NodeType.Generic
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                emotionCostContainer.style.display = selectedNode.NodeData.NodeType == NodeType.Emotion
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

                // Zero out the costs of the opposite type to prevent accidental data contamination
                if (selectedNode.NodeData.NodeType == NodeType.Generic) selectedNode.NodeData.Cost.EmotionPoints = 0;
                else selectedNode.NodeData.Cost.GenericPoints = 0;

                selectedNode.RefreshVisuals(); // Update the color!
            });
            inspectorPanel.Add(typeField);

            // --- GENERIC CONTAINER ---
            IntegerField genericCostField = new("Generic Cost") { value = selectedNode.NodeData.Cost.GenericPoints };
            genericCostField.RegisterValueChangedCallback(evt =>
                selectedNode.NodeData.Cost.GenericPoints = evt.newValue);
            genericCostContainer.Add(genericCostField);

            // Set initial visibility
            genericCostContainer.style.display = selectedNode.NodeData.NodeType == NodeType.Generic
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            inspectorPanel.Add(genericCostContainer);

            // --- EMOTION CONTAINER ---
            EnumField emotionTypeField = new("Emotion Color", selectedNode.NodeData.Cost.RequiredEmotion);
            emotionTypeField.RegisterValueChangedCallback(evt =>
            {
                selectedNode.NodeData.Cost.RequiredEmotion = (EmotionType)evt.newValue;
                selectedNode.RefreshVisuals(); // Change color instantly when dropdown changes
            });
            emotionCostContainer.Add(emotionTypeField);

            IntegerField emotionCostField = new("Emotion Cost") { value = selectedNode.NodeData.Cost.EmotionPoints };
            emotionCostField.RegisterValueChangedCallback(evt =>
                selectedNode.NodeData.Cost.EmotionPoints = evt.newValue);
            emotionCostContainer.Add(emotionCostField);

            // Set initial visibility
            emotionCostContainer.style.display = selectedNode.NodeData.NodeType == NodeType.Emotion
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            inspectorPanel.Add(emotionCostContainer);


            // --- 3. Abilities ---
            inspectorPanel.Add(new Label("Ability Unlocks")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 15, marginBottom = 5 } });

            Toggle abilityToggle = new("Unlocks Ability?") { value = selectedNode.NodeData.UnlocksAbility };
            abilityToggle.RegisterValueChangedCallback(evt => selectedNode.NodeData.UnlocksAbility = evt.newValue);
            inspectorPanel.Add(abilityToggle);

            TextField abilityIdField = new("Ability ID") { value = selectedNode.NodeData.GrantedAbilityId };
            abilityIdField.RegisterValueChangedCallback(evt => selectedNode.NodeData.GrantedAbilityId = evt.newValue);
            inspectorPanel.Add(abilityIdField);

            // --- 4. PASSIVE STATS ---
            inspectorPanel.Add(new Label("Granted Stats")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 15, marginBottom = 5 } });
            VisualElement statsContainer = new();
            inspectorPanel.Add(statsContainer);
            RedrawStatsList(selectedNode, statsContainer);

            Button addStatBtn = new(() =>
            {
                selectedNode.NodeData.GrantedStats.Add(new StatModifierData
                    { Stat = StatType.Damage, Type = ModifierType.Flat, Value = 0f });
                RedrawStatsList(selectedNode, statsContainer);
            })
            {
                text = "+ Add Stat Modifier",
                style = { marginTop = 5, backgroundColor = new Color(0.15f, 0.4f, 0.15f, 1f) }
            };
            inspectorPanel.Add(addStatBtn);
        }

        // Helper method to draw the dynamic list of stats
        private void RedrawStatsList(SkillNodeView selectedNode, VisualElement container)
        {
            container.Clear();

            List<StatModifierData> stats = selectedNode.NodeData.GrantedStats;

            for (int i = 0; i < stats.Count; i++)
            {
                int index = i; // Critical: Capture the index locally for the UI callbacks
                StatModifierData statMod = stats[index];

                // Create a horizontal row with a slightly lighter background to group the items
                VisualElement row = new()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        marginBottom = 5,
                        backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f),
                        paddingTop = 5, paddingBottom = 5, paddingLeft = 5, paddingRight = 5,
                        borderBottomLeftRadius = 3,
                        borderBottomRightRadius = 3,
                        borderTopLeftRadius = 3,
                        borderTopRightRadius = 3,
                        alignItems = Align.Center
                    }
                };

                // 1. Stat Enum Dropdown
                EnumField statDropdown = new(statMod.Stat) { style = { width = 100 } };
                statDropdown.RegisterValueChangedCallback(evt =>
                {
                    StatModifierData temp = stats[index]; // Copy struct
                    temp.Stat = (StatType)evt.newValue; // Modify copy
                    stats[index] = temp; // Save back to list
                });
                row.Add(statDropdown);

                // 2. Modifier Type Enum Dropdown
                EnumField typeDropdown = new(statMod.Type) { style = { width = 130 } };
                typeDropdown.RegisterValueChangedCallback(evt =>
                {
                    StatModifierData temp = stats[index];
                    temp.Type = (ModifierType)evt.newValue;
                    stats[index] = temp;
                });
                row.Add(typeDropdown);

                // 3. Value Input
                FloatField valueField = new() { value = statMod.Value, style = { flexGrow = 1, minWidth = 40 } };
                valueField.RegisterValueChangedCallback(evt =>
                {
                    StatModifierData temp = stats[index];
                    temp.Value = evt.newValue;
                    stats[index] = temp;
                });
                row.Add(valueField);

                // 4. Remove Button
                Button removeBtn = new(() =>
                {
                    stats.RemoveAt(index);
                    RedrawStatsList(selectedNode, container); // Refresh UI
                })
                {
                    text = "X",
                    style = { color = Color.red, unityFontStyleAndWeight = FontStyle.Bold, width = 25 }
                };
                row.Add(removeBtn);

                // Add the completed row to the container
                container.Add(row);
            }
        }
    }
}