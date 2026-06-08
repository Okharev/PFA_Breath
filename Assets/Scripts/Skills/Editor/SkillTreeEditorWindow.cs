using System;
using System.Collections.Generic;
using System.Linq;
using Ability.NewAbilitySystem; // NEW: Required for AbilityData
using Skills.Skills;
using Skills.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.Editor
{
    public class SkillTreeEditorWindow : EditorWindow
    {
        private SkillTreeGraph activeTreeAsset;

        // --- PREVIEW CONTAINERS ---
        private TwoPaneSplitView editContainer;
        private SkillTreeGraphView graphView;
        private ScrollView inspectorPanel;
        private bool isPreviewMode;
        private SkillTreeCanvas previewCanvas;
        private VisualElement previewContainer;
        private Texture2D previewBackgroundTexture;

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
            // --- 1. EDIT MODE CONTAINER (Graph + Inspector) ---
            editContainer = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            editContainer.style.flexGrow = 1;

            graphView = new SkillTreeGraphView { name = "Skill Tree Graph" };

            inspectorPanel = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            inspectorPanel.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            inspectorPanel.style.paddingLeft = 10;
            inspectorPanel.style.paddingTop = 10;
            inspectorPanel.Add(new Label("Select a node to edit properties.")
                { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            editContainer.Add(graphView);
            editContainer.Add(inspectorPanel);

            // --- 2. PREVIEW MODE CONTAINER (Runtime UI Canvas) ---
            previewContainer = new VisualElement();
            previewContainer.style.flexGrow = 1;
            previewContainer.style.display = DisplayStyle.None; 
            previewContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f); 
    
            // CRITICAL: Hide the portion of the map that extends beyond the editor window
            previewContainer.style.overflow = Overflow.Hidden;

            previewCanvas = new SkillTreeCanvas();
    
            // CRITICAL: Ensure the map zooms exactly from the center
            previewCanvas.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
    
            previewContainer.Add(previewCanvas);

            // CRITICAL FIX: Pass 'previewContainer' as the viewport, NOT 'graphView'. 
            // graphView has a width of 0 when preview mode is active!
            previewContainer.AddManipulator(new PanAndZoomManipulator(previewCanvas, previewContainer));

            // --- 3. ASSEMBLE ROOT ---
            rootVisualElement.Add(editContainer);
            rootVisualElement.Add(previewContainer);
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
            
            // --- NEW: MAP BACKGROUND SELECTOR ---
            ObjectField bgSelector = new("Preview Map")
            {
                objectType = typeof(Texture2D),
                allowSceneObjects = false,
                style = { width = 200, marginLeft = 10 }
            };

            bgSelector.RegisterValueChangedCallback(evt => 
            { 
                previewBackgroundTexture = evt.newValue as Texture2D;
    
                // --- NEW: Update the node editor canvas instantly! ---
                if (graphView != null) 
                {
                    graphView.SetMapBackground(previewBackgroundTexture);
                }
    
                // Keep your existing logic for the runtime preview mode
                if (isPreviewMode) 
                {
                    ApplyPreviewBackground();
                }
            });


            Button btnCreateGeneric = new(() => { CreateNewNode(typeof(GenericNodeData), "New Generic Skill"); })
                { text = "Create Generic Node" };
            Button btnCreateEmotion = new(() => { CreateNewNode(typeof(EmotionNodeData), "New Emotion Skill"); })
                { text = "Create Emotion Node" };

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

            // --- NEW PREVIEW TOGGLE BUTTON ---
            Button previewToggleBtn = new(TogglePreviewMode)
            {
                text = "Toggle UI Preview",
                style =
                {
                    backgroundColor = new Color(0.2f, 0.5f, 0.8f, 1f), color = Color.white,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };

            toolbar.Add(treeSelector);
            toolbar.Add(bgSelector);
            toolbar.Add(btnCreateGeneric);
            toolbar.Add(btnCreateEmotion);
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } }); // Spacer
            toolbar.Add(previewToggleBtn); // Add to right side
            toolbar.Add(saveButton);
            toolbar.Add(loadButton);

            rootVisualElement.Add(toolbar);
        }
        
        private void ApplyPreviewBackground()
        {
            if (previewBackgroundTexture != null)
            {
                previewCanvas.style.backgroundImage = new StyleBackground(previewBackgroundTexture);
        
                // --- SYNC WITH RUNTIME LOGIC ---
                // 1. Explicitly prevent Flexbox from squishing the canvas in small editor windows
                previewCanvas.style.flexShrink = 0; 
                previewCanvas.style.flexGrow = 0;
        
                // 2. Lock the exact texture proportions
                previewCanvas.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;

                // 3. Lock 1:1 Resolution to perfectly align nodes with drawn map features
                previewCanvas.style.width = previewBackgroundTexture.width;
                previewCanvas.style.height = previewBackgroundTexture.height;
            }
            else
            {
                // Fallback to responsive scaling if no map is assigned
                previewCanvas.style.backgroundImage = null;
                previewCanvas.style.width = StyleKeyword.Auto;
                previewCanvas.style.height = StyleKeyword.Auto;
        
                // Reset Flexbox states
                previewCanvas.style.flexShrink = 1; 
                previewCanvas.style.flexGrow = 1; 
            }
        }

        private void TogglePreviewMode()
        {
            if (activeTreeAsset == null)
            {
                Debug.LogWarning("[Skill Tree Editor] Please load a graph before previewing.");
                return;
            }

            isPreviewMode = !isPreviewMode;

            if (isPreviewMode)
            {
                GraphSaveUtility.GetInstance(graphView, activeTreeAsset).SaveGraph();

                editContainer.style.display = DisplayStyle.None;
                previewContainer.style.display = DisplayStyle.Flex;

                previewCanvas.Populate(activeTreeAsset, true);
        
                // NEW: Apply the texture layout boundaries!
                ApplyPreviewBackground(); 
            }
            else
            {
                previewContainer.style.display = DisplayStyle.None;
                editContainer.style.display = DisplayStyle.Flex;
            }
        }

        private void CreateNewNode(Type type, string defaultName)
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
            nameField.RegisterValueChangedCallback(evt =>
            {
                selectedNode.NodeData.NodeName = evt.newValue;
                selectedNode.title = evt.newValue;
            });
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
            inspectorPanel.Add(new Label("Generic Properties")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 15 } });

            IntegerField costField = new("Generic Cost") { value = data.GenericCost };
            costField.RegisterValueChangedCallback(evt => data.GenericCost = evt.newValue);
            inspectorPanel.Add(costField);
        }

        private void DrawEmotionNodeInspector(SkillNodeView node, EmotionNodeData data)
        {
            inspectorPanel.Add(new Label("Emotion Properties")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 15 } });

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

            Slider rotationSlider = new Slider("Orbit Rotation", 0, 360) { value = data.OrbitRotation };
            rotationSlider.RegisterValueChangedCallback(evt => 
            {
                data.OrbitRotation = evt.newValue;
        
                // 1. Update the Graph View node visuals if needed (optional)
                node.RefreshVisuals(); 
        
                // 2. Trigger the refresh on the active Preview Canvas
                RefreshPreviewNode(data.GUID); 
            });
            inspectorPanel.Add(rotationSlider);

            Slider spanSlider = new Slider("Orbit Span", 0, 180) { value = data.OrbitSpan };
            spanSlider.RegisterValueChangedCallback(evt => 
            {
                data.OrbitSpan = evt.newValue;
        
                // 1. Update the Graph View node visuals
                node.RefreshVisuals();
        
                // 2. Trigger the refresh on the active Preview Canvas
                RefreshPreviewNode(data.GUID);
            });
            inspectorPanel.Add(spanSlider);
            
            Toggle abilityToggle = new("Unlocks Ability?") { value = data.UnlocksAbility };
            abilityToggle.RegisterValueChangedCallback(evt => data.UnlocksAbility = evt.newValue);
            inspectorPanel.Add(abilityToggle);

            // --- REFACTORED: Changed from a String TextField to a ScriptableObject Drag & Drop Field ---
            ObjectField abilityDataField = new("Granted Ability") 
            { 
                objectType = typeof(AbilityData), 
                value = data.GrantedAbility, 
                allowSceneObjects = false 
            };
            abilityDataField.RegisterValueChangedCallback(evt => data.GrantedAbility = evt.newValue as AbilityData);
            inspectorPanel.Add(abilityDataField);
            // -----------------------------------------------------------------------------------------

            EnumField intendedSlotField = new("Intended Slot", data.IntendedSlot);
            intendedSlotField.RegisterValueChangedCallback(evt => data.IntendedSlot = (AbilitySlot)evt.newValue);
            inspectorPanel.Add(intendedSlotField);
        }

        private void RefreshPreviewNode(string nodeGuid)
        {
            // If we aren't in preview mode, don't worry about updating the visuals
            if (previewCanvas == null) return;

            // Search through the children of the canvas to find the matching node
            foreach (var element in previewCanvas.Children())
            {
                if (element is EmotionSkillNodeView emotionNode && emotionNode.NodeData.GUID == nodeGuid)
                {
                    // This is the specific method you need to call
                    emotionNode.GenerateOrbitalIndicators(); 
                    return;
                }
            }
        }
        
        // Separated Stats Drawing to pass the specific stat list directly
        private void DrawStatsSection(List<StatModifierData> statList)
        {
            inspectorPanel.Add(new Label("Granted Stats")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 15, marginBottom = 5 } });
            VisualElement statsContainer = new();
            inspectorPanel.Add(statsContainer);
            RedrawStatsList(statList, statsContainer);

            Button addStatBtn = new(() =>
            {
                statList.Add(new StatModifierData { Stat = StatType.Damage, Type = ModifierType.Flat, Value = 0f });
                RedrawStatsList(statList, statsContainer);
            })
            {
                text = "+ Add Stat Modifier",
                style = { marginTop = 5, backgroundColor = new Color(0.15f, 0.4f, 0.15f, 1f) }
            };
            inspectorPanel.Add(addStatBtn);
        }

        private static void RedrawStatsList(List<StatModifierData> stats, VisualElement container)
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
                statDropdown.RegisterValueChangedCallback(evt =>
                {
                    StatModifierData t = stats[index];
                    t.Stat = (StatType)evt.newValue;
                    stats[index] = t;
                });

                EnumField typeDropdown = new(statMod.Type) { style = { width = 130 } };
                typeDropdown.RegisterValueChangedCallback(evt =>
                {
                    StatModifierData t = stats[index];
                    t.Type = (ModifierType)evt.newValue;
                    stats[index] = t;
                });

                FloatField valueField = new() { value = statMod.Value, style = { flexGrow = 1, minWidth = 40 } };
                valueField.RegisterValueChangedCallback(evt =>
                {
                    StatModifierData t = stats[index];
                    t.Value = evt.newValue;
                    stats[index] = t;
                });

                Button removeBtn = new(() =>
                    {
                        stats.RemoveAt(index);
                        RedrawStatsList(stats, container);
                    })
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