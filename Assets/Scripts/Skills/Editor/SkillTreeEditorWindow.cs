using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Skills.Skills;

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
            ConstructLayout();
        }

        [MenuItem("Window/Custom Tools/Skill Tree Editor")]
        public static void OpenWindow()
        {
            SkillTreeEditorWindow window = GetWindow<SkillTreeEditorWindow>();
            window.titleContent = new GUIContent("Skill Tree Editor");
        }

        private void ConstructLayout()
        {
            TwoPaneSplitView splitView = new(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;

            graphView = new SkillTreeGraphView { name = "Skill Tree Graph" };

            inspectorPanel = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            inspectorPanel.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            inspectorPanel.style.paddingLeft = 10;
            inspectorPanel.style.paddingTop = 10;
            inspectorPanel.Add(new Label("Select a node to edit properties.") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            splitView.Add(graphView);
            splitView.Add(inspectorPanel);
            rootVisualElement.Add(splitView);
        }

        private void GenerateToolbar()
        {
            Toolbar toolbar = new();

            ObjectField treeSelector = new("Active Tree")
            {
                objectType = typeof(SkillTreeGraph),
                allowSceneObjects = false,
                value = activeTreeAsset
            };
            treeSelector.RegisterValueChangedCallback(evt => { activeTreeAsset = evt.newValue as SkillTreeGraph; });

            Button btnCreateGeneric = new(() => { CreateNewNode(typeof(GenericNodeData), "New Generic Skill"); }) { text = "Create Generic Node" };
            Button btnCreateEmotion = new(() => { CreateNewNode(typeof(EmotionNodeData), "New Emotion Skill"); }) { text = "Create Emotion Node" };

            Button saveButton = new(() =>
            {
                if (activeTreeAsset != null) GraphSaveUtility.GetInstance(graphView, activeTreeAsset).SaveGraph();
            }) { text = "Save Graph" };

            Button loadButton = new(() =>
            {
                if (activeTreeAsset != null)
                {
                    GraphSaveUtility.GetInstance(graphView, activeTreeAsset).LoadGraph();
                    foreach (SkillNodeView node in graphView.nodes.ToList().Cast<SkillNodeView>())
                        node.OnNodeSelected += UpdateInspector;
                    inspectorPanel.Clear();
                }
            }) { text = "Load Graph" };

            toolbar.Add(treeSelector);
            toolbar.Add(btnCreateGeneric);
            toolbar.Add(btnCreateEmotion);
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } }); // Spacer
            toolbar.Add(saveButton);
            toolbar.Add(loadButton);

            rootVisualElement.Add(toolbar);
        }

        private void CreateNewNode(System.Type type, string defaultName)
        {
            if (graphView == null) return;
            SkillNodeView newNode = graphView.CreateNode(type, defaultName);
            newNode.OnNodeSelected += UpdateInspector;
        }

        private void UpdateInspector(SkillNodeView selectedNode)
        {
            inspectorPanel.Clear();
            if (selectedNode == null) return;

            // --- 1. BASE IDENTITY (Shared across all node types) ---
            Label header = new($"Editing: {selectedNode.NodeData.NodeName}")
                { style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 10 } };
            inspectorPanel.Add(header);

            TextField nameField = new("Node Name") { value = selectedNode.NodeData.NodeName };
            nameField.RegisterValueChangedCallback(evt => { selectedNode.NodeData.NodeName = evt.newValue; selectedNode.title = evt.newValue; });
            inspectorPanel.Add(nameField);

            TextField descField = new("Description") { value = selectedNode.NodeData.Description, multiline = true };
            descField.RegisterValueChangedCallback(evt => selectedNode.NodeData.Description = evt.newValue);
            inspectorPanel.Add(descField);

            // --- 2. POLYMORPHIC DRAWING ---
            if (selectedNode.NodeData is GenericNodeData genericData)
            {
                DrawGenericNodeInspector(genericData);
                DrawStatsSection(genericData.GrantedStats);
            }
            else if (selectedNode.NodeData is EmotionNodeData emotionData)
            {
                DrawEmotionNodeInspector(selectedNode, emotionData);
                DrawStatsSection(emotionData.GrantedStats);
            }
        }

        private void DrawGenericNodeInspector(GenericNodeData data)
        {
            inspectorPanel.Add(new Label("Generic Properties") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 15 } });
            
            IntegerField costField = new("Generic Cost") { value = data.GenericCost };
            costField.RegisterValueChangedCallback(evt => data.GenericCost = evt.newValue);
            inspectorPanel.Add(costField);
        }

        private void DrawEmotionNodeInspector(SkillNodeView node, EmotionNodeData data)
        {
            inspectorPanel.Add(new Label("Emotion Properties") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 15 } });

            EnumField emotionTypeField = new("Emotion Color", data.RequiredEmotion);
            emotionTypeField.RegisterValueChangedCallback(evt =>
            {
                data.RequiredEmotion = (EmotionType)evt.newValue;
                node.RefreshVisuals(); 
            });
            inspectorPanel.Add(emotionTypeField);

            IntegerField costField = new("Base Emotion Cost") { value = data.BaseEmotionCost };
            costField.RegisterValueChangedCallback(evt => data.BaseEmotionCost = evt.newValue);
            inspectorPanel.Add(costField);

            IntegerField maxLevelField = new("Max Level") { value = data.MaxLevel };
            maxLevelField.RegisterValueChangedCallback(evt => data.MaxLevel = evt.newValue);
            inspectorPanel.Add(maxLevelField);

            Toggle abilityToggle = new("Unlocks Ability?") { value = data.UnlocksAbility };
            abilityToggle.RegisterValueChangedCallback(evt => data.UnlocksAbility = evt.newValue);
            inspectorPanel.Add(abilityToggle);

            TextField abilityIdField = new("Granted Ability ID") { value = data.GrantedAbilityId };
            abilityIdField.RegisterValueChangedCallback(evt => data.GrantedAbilityId = evt.newValue);
            inspectorPanel.Add(abilityIdField);
        }

        // Separated Stats Drawing to pass the specific stat list directly
        private void DrawStatsSection(List<StatModifierData> statList)
        {
            inspectorPanel.Add(new Label("Granted Stats") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 15, marginBottom = 5 } });
            VisualElement statsContainer = new();
            inspectorPanel.Add(statsContainer);
            RedrawStatsList(statList, statsContainer);

            Button addStatBtn = new(() =>
            {
                statList.Add(new StatModifierData { Stat = StatType.Damage, Type = ModifierType.Flat, Value = 0f });
                RedrawStatsList(statList, statsContainer);
            })
            { text = "+ Add Stat Modifier", style = { marginTop = 5, backgroundColor = new Color(0.15f, 0.4f, 0.15f, 1f) } };
            inspectorPanel.Add(addStatBtn);
        }

        private void RedrawStatsList(List<StatModifierData> stats, VisualElement container)
        {
            container.Clear();
            for (int i = 0; i < stats.Count; i++)
            {
                int index = i; 
                StatModifierData statMod = stats[index];

                VisualElement row = new()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row, marginBottom = 5,
                        backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f),
                        paddingTop = 5, paddingBottom = 5, paddingLeft = 5, paddingRight = 5,
                        alignItems = Align.Center
                    }
                };

                EnumField statDropdown = new(statMod.Stat) { style = { width = 100 } };
                statDropdown.RegisterValueChangedCallback(evt => { var t = stats[index]; t.Stat = (StatType)evt.newValue; stats[index] = t; });
                
                EnumField typeDropdown = new(statMod.Type) { style = { width = 130 } };
                typeDropdown.RegisterValueChangedCallback(evt => { var t = stats[index]; t.Type = (ModifierType)evt.newValue; stats[index] = t; });

                FloatField valueField = new() { value = statMod.Value, style = { flexGrow = 1, minWidth = 40 } };
                valueField.RegisterValueChangedCallback(evt => { var t = stats[index]; t.Value = evt.newValue; stats[index] = t; });

                Button removeBtn = new(() => { stats.RemoveAt(index); RedrawStatsList(stats, container); })
                    { text = "X", style = { color = Color.red, unityFontStyleAndWeight = FontStyle.Bold, width = 25 } };

                row.Add(statDropdown);
                row.Add(typeDropdown);
                row.Add(valueField);
                row.Add(removeBtn);
                container.Add(row);
            }
        }
    }
}