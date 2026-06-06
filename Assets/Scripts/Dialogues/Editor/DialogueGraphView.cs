using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dialogues.Editor
{
    public class DialogueGraphView : GraphView
    {
        private DialogueEditorWindow _window;
        public Action<DialogueNodeView> OnNodeSelected;

        public DialogueGraphView(DialogueEditorWindow window)
        {
            _window = window;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            GridBackground grid = new();
            grid.StretchToParentSize();
            Insert(0, grid);

            graphViewChanged = OnGraphViewChanged;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            // 1. Handle Connections (Edges Created)
            if (graphViewChange.edgesToCreate != null)
                foreach (Edge edge in graphViewChange.edgesToCreate)
                {
                    DialogueNodeView parentView = edge.output.node as DialogueNodeView;
                    DialogueNodeView childView = edge.input.node as DialogueNodeView;

                    if (parentView != null && childView != null)
                    {
                        // Check if the wire came from a Choice Port
                        if (edge.output.userData is DialogueChoice choiceData)
                            choiceData.nextNode = childView.NodeData;
                        // Or if the wire came from the Default "Next Node" Port
                        else if (edge.output == parentView.DefaultOutputPort)
                            parentView.NodeData.nextNode = childView.NodeData;

                        // Tell Unity this ScriptableObject changed so it saves to disk
                        EditorUtility.SetDirty(parentView.NodeData);
                    }
                }

            // 2. Handle Disconnections (Edges Removed)
            if (graphViewChange.elementsToRemove != null)
                foreach (GraphElement element in graphViewChange.elementsToRemove)
                    if (element is Edge edge)
                    {
                        DialogueNodeView parentView = edge.output.node as DialogueNodeView;

                        if (parentView != null)
                        {
                            // If a choice port wire was deleted
                            if (edge.output.userData is DialogueChoice choiceData)
                                choiceData.nextNode = null;
                            // If the default port wire was deleted
                            else if (edge.output == parentView.DefaultOutputPort) parentView.NodeData.nextNode = null;

                            EditorUtility.SetDirty(parentView.NodeData);
                        }
                    }

            return graphViewChange;
        }

        // --- NEW: Add the Right-Click Menu ---
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            // Convert screen mouse position to local graph coordinates
            Vector2 localMousePos = contentViewContainer.WorldToLocal(evt.mousePosition);

            // Append our custom action to the right-click menu
            evt.menu.AppendAction("Add Dialogue Node", action => CreateNewNode("New Node", localMousePos));
        }

        private void CreateNewNode(string nodeName, Vector2 position)
        {
            // 1. Instantiate the backing ScriptableObject model
            DialogueNode newNodeData = ScriptableObject.CreateInstance<DialogueNode>();
            newNodeData.name = nodeName;
            newNodeData.text = "Enter dialogue here...";

            // 2. Create the visual representation
            DialogueNodeView nodeView = new(newNodeData);
            nodeView.SetPosition(new Rect(position, Vector2.zero));

            // Pass the selection callback down to the node
            nodeView.OnSelectedCallback = OnNodeSelected;

            AddElement(nodeView);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            List<Port> compatiblePorts = new();

            // Using .ToList() forces the graph to freshly evaluate all visual elements, 
            // guaranteeing it finds your newly generated choice pins!
            foreach (Port port in ports.ToList())
            {
                // 1. Prevent connecting to itself
                if (startPort == port) continue;

                // 2. Prevent connecting to a different port on the exact same node
                if (startPort.node == port.node) continue;

                // 3. Prevent connecting Output to Output, or Input to Input
                if (startPort.direction == port.direction) continue;

                // 4. Ensure the port data types match exactly
                if (startPort.portType != port.portType) continue;

                compatiblePorts.Add(port);
            }

            return compatiblePorts;
        }
    }
}