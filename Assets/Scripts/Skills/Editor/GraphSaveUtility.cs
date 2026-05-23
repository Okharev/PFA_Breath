using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Skills.Skills; // Required

namespace Skills.Editor
{
    public class GraphSaveUtility
    {
        private SkillTreeGraph targetContainer;
        private SkillTreeGraphView targetGraphView;

        private List<Edge> Edges => targetGraphView.edges.ToList();
        private List<SkillNodeView> Nodes => targetGraphView.nodes.ToList().Cast<SkillNodeView>().ToList();

        public static GraphSaveUtility GetInstance(SkillTreeGraphView graphView, SkillTreeGraph container)
        {
            return new GraphSaveUtility { targetGraphView = graphView, targetContainer = container };
        }

        public void SaveGraph()
        {
            if (targetContainer == null) return;
            targetContainer.AllNodes.Clear();

            foreach (SkillNodeView nodeView in Nodes)
            {
                nodeView.NodeData.Position = nodeView.GetPosition();
                nodeView.NodeData.PrerequisiteGUIDs.Clear();
                targetContainer.AllNodes.Add(nodeView.NodeData);
            }

            Edge[] connectedPorts = Edges.Where(x => x.input.node != null).ToArray();
            foreach (Edge edge in connectedPorts)
            {
                SkillNodeView outputNode = edge.output.node as SkillNodeView; 
                SkillNodeView inputNode = edge.input.node as SkillNodeView; 

                inputNode.NodeData.PrerequisiteGUIDs.Add(outputNode.NodeData.GUID);
            }

            EditorUtility.SetDirty(targetContainer);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Skill Tree] Saved {targetContainer.AllNodes.Count} nodes to {targetContainer.name}.");
        }

        public void LoadGraph()
        {
            if (targetContainer == null) return;
            ClearGraph();

            Dictionary<string, SkillNodeView> nodeDictionary = new();

            // 1. REBUILD NODES (Automatically handling derived instances)
            foreach (BaseNodeData nodeData in targetContainer.AllNodes)
            {
                SkillNodeView nodeView = targetGraphView.CreateNode(nodeData);
                nodeDictionary.Add(nodeData.GUID, nodeView);
            }

            // 2. REBUILD WIRES
            foreach (SkillNodeView nodeView in Nodes)
            foreach (string requiredGuid in nodeView.NodeData.PrerequisiteGUIDs)
                if (nodeDictionary.TryGetValue(requiredGuid, out SkillNodeView requiredNodeView))
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
            foreach (SkillNodeView node in Nodes) targetGraphView.RemoveElement(node);
            foreach (Edge edge in Edges) targetGraphView.RemoveElement(edge);
        }
    }
}