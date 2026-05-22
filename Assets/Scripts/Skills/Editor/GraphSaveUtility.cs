using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.Editor
{
    public class GraphSaveUtility
    {
        private SkillTreeGraph targetContainer;
        private SkillTreeGraphView targetGraphView;

        // LINQ helpers to quickly grab all visual elements on the canvas
        private List<Edge> Edges => targetGraphView.edges.ToList();
        private List<SkillNodeView> Nodes => targetGraphView.nodes.ToList().Cast<SkillNodeView>().ToList();

        public static GraphSaveUtility GetInstance(SkillTreeGraphView graphView, SkillTreeGraph container)
        {
            return new GraphSaveUtility
            {
                targetGraphView = graphView,
                targetContainer = container
            };
        }

        public void SaveGraph()
        {
            if (targetContainer == null) return;

            targetContainer.AllNodes.Clear();

            // --- 1. SAVE THE NODES ---
            foreach (SkillNodeView nodeView in Nodes)
            {
                // Update the position so it loads exactly where we left it
                nodeView.NodeData.Position = nodeView.GetPosition();

                // Clear old wires, we will rebuild them below
                nodeView.NodeData.PrerequisiteGUIDs.Clear();

                targetContainer.AllNodes.Add(nodeView.NodeData);
            }

            // --- 2. SAVE THE WIRES (EDGES) ---
            Edge[] connectedPorts = Edges.Where(x => x.input.node != null).ToArray();
            foreach (Edge edge in connectedPorts)
            {
                SkillNodeView outputNode = edge.output.node as SkillNodeView; // The prerequisite node
                SkillNodeView inputNode = edge.input.node as SkillNodeView; // The node being unlocked

                // Add the Prerequisite's GUID to the Input node's data
                inputNode.NodeData.PrerequisiteGUIDs.Add(outputNode.NodeData.GUID);
            }

            // --- 3. WRITE TO DISK ---
            EditorUtility.SetDirty(targetContainer);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Skill Tree] Saved {targetContainer.AllNodes.Count} nodes to {targetContainer.name}.");
        }

        public void LoadGraph()
        {
            if (targetContainer == null) return;
            ClearGraph();

            // --- 1. REBUILD NODES ---
            // We use a dictionary to cache them by GUID for O(1) lookups when reconnecting wires
            Dictionary<string, SkillNodeView> nodeDictionary = new();

            foreach (SkillNodeData nodeData in targetContainer.AllNodes)
            {
                // We pass the existing data to CreateNode so it doesn't generate a blank one
                SkillNodeView nodeView = targetGraphView.CreateNode(nodeData);
                nodeDictionary.Add(nodeData.GUID, nodeView);
            }

            // --- 2. REBUILD WIRES ---
            foreach (SkillNodeView nodeView in Nodes)
            foreach (string requiredGuid in nodeView.NodeData.PrerequisiteGUIDs)
                if (nodeDictionary.TryGetValue(requiredGuid, out SkillNodeView requiredNodeView))
                    // Link the Output port of the prerequisite to the Input port of the current node
                    LinkNodes(requiredNodeView.outputContainer.Q<Port>(), nodeView.inputContainer.Q<Port>());

            Debug.Log($"[Skill Tree] Loaded {targetContainer.AllNodes.Count} nodes from {targetContainer.name}.");
        }

        private void LinkNodes(Port output, Port input)
        {
            Edge edge = new() { output = output, input = input };
            edge.input.Connect(edge);
            edge.output.Connect(edge);
            targetGraphView.AddElement(edge);
        }

        private void ClearGraph()
        {
            // Destroy all visual elements before loading a new tree
            foreach (SkillNodeView node in Nodes) targetGraphView.RemoveElement(node);
            foreach (Edge edge in Edges) targetGraphView.RemoveElement(edge);
        }
    }
}