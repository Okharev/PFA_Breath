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
        public SkillTreeGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            GridBackground grid = new();
            Insert(0, grid);
            grid.StretchToParentSize();

            StyleSheet styleSheet =
                AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Skills/Editor/SkillTreeGraph.uss");
            if (styleSheet != null) styleSheets.Add(styleSheet);
        }

        // 1. Creation from Load
        public SkillNodeView CreateNode(BaseNodeData existingData)
        {
            SkillNodeView node = new(existingData);
            node.SetPosition(existingData.Position);
            AddElement(node);
            return node;
        }

        // 2. Creation from Editor Button (Polymorphic Factory)
        public SkillNodeView CreateNode(Type nodeDataType, string defaultName)
        {
            // Dynamically instantiate the requested subclass
            BaseNodeData newData = (BaseNodeData)Activator.CreateInstance(nodeDataType);
            newData.NodeName = defaultName;

            SkillNodeView node = new(newData);
            node.SetPosition(new Rect(100, 100, 200, 150)); // Default spawn pos
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