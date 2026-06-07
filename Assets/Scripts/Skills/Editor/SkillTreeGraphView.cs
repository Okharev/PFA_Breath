using System;
using System.Collections.Generic;
using Skills.Skills;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.Editor
{
    public class SkillTreeGraphView : GraphView
    {
        private VisualElement mapBackgroundLayer; // NEW: The layer that holds the map
        private GridBackground grid;              // We cache this to hide it when the map is active

        public SkillTreeGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // Cache the grid so we can toggle it later
            grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // --- NEW: INJECT THE MAP BACKGROUND LAYER ---
            mapBackgroundLayer = new VisualElement
            {
                pickingMode = PickingMode.Ignore // CRITICAL: Ensures you can still click/drag nodes over the image
            };
            
            // Insert it at index 0 of the contentViewContainer. 
            // This ensures it sits behind all the nodes and connection wires.
            contentViewContainer.Insert(0, mapBackgroundLayer);
            // ---------------------------------------------

            StyleSheet styleSheet =
                AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Skills/Editor/SkillTreeGraph.uss");
            if (styleSheet != null) styleSheets.Add(styleSheet);
        }

        // --- NEW: METHOD TO APPLY THE TEXTURE ---
        public void SetMapBackground(Texture2D mapTex)
        {
            if (mapTex != null)
            {
                mapBackgroundLayer.style.backgroundImage = new StyleBackground(mapTex);
                mapBackgroundLayer.style.width = mapTex.width;
                mapBackgroundLayer.style.height = mapTex.height;
        
                // Pin it firmly to (0,0) in the node graph space
                mapBackgroundLayer.style.position = Position.Absolute;
                mapBackgroundLayer.style.left = 0;
                mapBackgroundLayer.style.top = 0;
        
                // --- THE FIX ---
                // Forces the UI Toolkit rendering engine to push this element to Index 0 
                // of the container, guaranteeing it draws BEFORE the nodes and edges.
                mapBackgroundLayer.SendToBack();
                // ---------------
        
                // Hide the dotted grid so it doesn't clash with your artwork
                grid.style.display = DisplayStyle.None;
            }
            else
            {
                // Revert to the default grid if the map is cleared
                mapBackgroundLayer.style.backgroundImage = null;
                grid.style.display = DisplayStyle.Flex;
            }
        }
        // ----------------------------------------

        public SkillNodeView CreateNode(BaseNodeData existingData)
        {
            SkillNodeView node = new(existingData);
            node.SetPosition(existingData.Position);
            AddElement(node);
            return node;
        }

        public SkillNodeView CreateNode(Type nodeDataType, string defaultName)
        {
            BaseNodeData newData = (BaseNodeData)Activator.CreateInstance(nodeDataType);
            newData.NodeName = defaultName;

            SkillNodeView node = new(newData);
            node.SetPosition(new Rect(100, 100, 200, 150)); 
            AddElement(node);
            return node;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            List<Port> compatiblePorts = new();
            foreach (Port port in ports)
                if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
                    compatiblePorts.Add(port);
            return compatiblePorts;
        }
    }
}