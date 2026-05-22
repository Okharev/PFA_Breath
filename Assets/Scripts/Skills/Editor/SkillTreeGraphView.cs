using System.Collections.Generic;
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

            if (styleSheet != null)
                styleSheets.Add(styleSheet);
            else
                Debug.LogWarning(
                    "[Skill Tree Tool] Could not find SkillTreeGraph.uss! Ensure the file exists at Assets/Scripts/Skills/Editor/SkillTreeGraph.uss");
        }

        public SkillNodeView CreateNode(SkillNodeData existingData)
        {
            SkillNodeView node = new(existingData.NodeName);
            node.NodeData = existingData;
            node.SetPosition(existingData.Position);

            node.RefreshVisuals(); 

            AddElement(node);
            return node;
        }

// Keep your existing CreateNode method for when clicking the "Create Node" button
        public SkillNodeView CreateNode(string nodeName)
        {
            SkillNodeView node = new(nodeName);
            node.SetPosition(new Rect(100, 100, 200, 150));
            AddElement(node);
            return node;
        }

        // Logic preventing invalid wire connections (e.g., Input to Input, or a node to itself)
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            List<Port> compatiblePorts = new();
            foreach (Port port in ports)
            {
                if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
                    compatiblePorts.Add(port);
            }

            return compatiblePorts;
        }
    }
}