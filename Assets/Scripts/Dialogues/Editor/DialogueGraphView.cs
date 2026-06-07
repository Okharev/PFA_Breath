using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dialogues.Editor
{
    /// <summary>
    ///     Intercepts wires dropped into empty space and commands the GraphView to spawn a node.
    /// </summary>
    public class DialogueEdgeListener : IEdgeConnectorListener
    {
        private readonly DialogueGraphView _graphView;

        public DialogueEdgeListener(DialogueGraphView graphView)
        {
            _graphView = graphView;
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            // 1. Convert the window mouse position to the graph's local zoom/pan coordinates
            Vector2 localMousePos = _graphView.contentViewContainer.WorldToLocal(position);

            // 2. Identify which port the user dragged the wire FROM
            Port draggedPort = edge.output != null ? edge.output : edge.input;

            // 3. Command the graph to spawn and link
            _graphView.SpawnNodeFromEdge(draggedPort, localMousePos);
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            // We leave this blank. GraphView natively handles dropping a wire ONTO a valid port.
        }
    }

    public class DialogueGraphView : GraphView
    {
        private readonly DialogueEditorWindow _window;
        public Action<DialogueNodeView> OnNodeSelected;

        public DialogueGraphView(DialogueEditorWindow window)
        {
            _window = window;

            // Initialize the listener
            EdgeListener = new DialogueEdgeListener(this);

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            GridBackground grid = new();
            grid.StretchToParentSize();
            Insert(0, grid);

            graphViewChanged = OnGraphViewChanged;

            nodeCreationRequest = RequestNodeCreation;
        }

        public DialogueEdgeListener EdgeListener { get; }

        // --- NEW: Called by the DialogueEdgeListener when a wire is dropped ---
        public void SpawnNodeFromEdge(Port draggedPort, Vector2 position)
        {
            // 1. Spawn the node exactly at the mouse cursor
            DialogueNodeView newNode = CreateNewNode("New Node", position);

            // 2. Auto-connect the wire
            if (draggedPort.direction == Direction.Output)
            {
                // Dragged from Parent to Child
                LinkPorts(draggedPort, newNode.InputPort);

                // Update backing game data
                DialogueNodeView parentView = draggedPort.node as DialogueNodeView;
                if (draggedPort.userData is DialogueChoice choiceData)
                    choiceData.nextNode = newNode.NodeData;
                else if (draggedPort == parentView.DefaultOutputPort)
                    parentView.NodeData.nextNode = newNode.NodeData;

                EditorUtility.SetDirty(parentView.NodeData);
            }
            else
            {
                // Dragged backwards from Child (Input) to Parent (Output)
                LinkPorts(newNode.DefaultOutputPort, draggedPort);

                // Update backing game data
                newNode.NodeData.nextNode = (draggedPort.node as DialogueNodeView).NodeData;
                EditorUtility.SetDirty(newNode.NodeData);
            }
        }

        private void RequestNodeCreation(NodeCreationContext context)
        {
            // 1. Calculate correct graph-local mouse position
            Vector2 windowMousePos = context.screenMousePosition - _window.position.position;
            Vector2 localMousePos = contentViewContainer.WorldToLocal(windowMousePos);

            // 2. Spawn the node automatically
            DialogueNodeView newNode = CreateNewNode("New Node", localMousePos);

            // 3. If the user dragged a wire to create this node, auto-connect it!
            if (context.target is Port connectedPort)
            {
                if (connectedPort.direction == Direction.Output)
                {
                    // Dragged from Parent to Child
                    LinkPorts(connectedPort, newNode.InputPort);

                    // Update backing game data
                    DialogueNodeView parentView = connectedPort.node as DialogueNodeView;
                    if (connectedPort.userData is DialogueChoice choiceData)
                        choiceData.nextNode = newNode.NodeData;
                    else if (connectedPort == parentView.DefaultOutputPort)
                        parentView.NodeData.nextNode = newNode.NodeData;

                    EditorUtility.SetDirty(parentView.NodeData);
                }
                else if (connectedPort.direction == Direction.Input)
                {
                    // Dragged backwards from Child to Parent
                    LinkPorts(newNode.DefaultOutputPort, connectedPort);

                    // Update backing game data
                    newNode.NodeData.nextNode = (connectedPort.node as DialogueNodeView).NodeData;
                    EditorUtility.SetDirty(newNode.NodeData);
                }
            }
        }

        public void AutoLayoutNodes()
        {
            List<DialogueNodeView> allNodes = nodes.ToList().Cast<DialogueNodeView>().ToList();
            if (allNodes.Count == 0) return;

            // 1. Find the Root Nodes
            List<DialogueNodeView> roots = allNodes.Where(n => !n.InputPort.connected).ToList();
            if (roots.Count == 0) roots.Add(allNodes[0]);

            // ==========================================
            // X-AXIS: Calculate Longest Path (Depth)
            // ==========================================
            Dictionary<DialogueNodeView, int> nodeDepths = new();
            foreach (DialogueNodeView node in allNodes) nodeDepths[node] = 0;

            Queue<DialogueNodeView> depthQueue = new(roots);

            while (depthQueue.Count > 0)
            {
                DialogueNodeView current = depthQueue.Dequeue();
                int currentDepth = nodeDepths[current];

                foreach (DialogueNodeView child in GetChildren(current))
                    // If we found a LONGER path, we must push the node further right
                    if (currentDepth + 1 > nodeDepths[child])
                        // Failsafe: Prevent infinite loops if the user made a circular dialogue tree
                        if (currentDepth < allNodes.Count)
                        {
                            nodeDepths[child] = currentDepth + 1;

                            // Re-evaluate this node's children to push them right as well
                            if (!depthQueue.Contains(child)) depthQueue.Enqueue(child);
                        }
            }

            // ==========================================
            // Y-AXIS: Assign Unique Horizontal Lanes
            // ==========================================
            Dictionary<DialogueNodeView, int> nodeLanes = new();
            int nextAvailableLane = 0;

            Queue<DialogueNodeView> laneQueue = new();
            HashSet<DialogueNodeView> laneVisited = new();

            foreach (DialogueNodeView root in roots)
            {
                nodeLanes[root] = nextAvailableLane++;
                laneQueue.Enqueue(root);
                laneVisited.Add(root);
            }

            while (laneQueue.Count > 0)
            {
                DialogueNodeView current = laneQueue.Dequeue();
                int parentLane = nodeLanes[current];

                List<DialogueNodeView> children = GetChildren(current);
                bool isFirstChild = true;

                foreach (DialogueNodeView child in children)
                    if (laneVisited.Add(child))
                    {
                        if (isFirstChild)
                        {
                            // The primary choice continues straight on the parent's lane
                            nodeLanes[child] = parentLane;
                            isFirstChild = false;
                        }
                        else
                        {
                            // Alternate choices are forced into brand new lanes!
                            nodeLanes[child] = nextAvailableLane++;
                        }

                        laneQueue.Enqueue(child);
                    }
            }

            // Failsafe: Handle floating nodes that weren't connected to the main tree
            foreach (DialogueNodeView node in allNodes)
                if (!laneVisited.Contains(node))
                    nodeLanes[node] = nextAvailableLane++;

            // ==========================================
            // APPLY POSITIONS
            // ==========================================
            float horizontalSpacing = 350f;
            float verticalSpacing = 160f; // Slightly smaller to accommodate the new lanes

            foreach (DialogueNodeView node in allNodes)
            {
                float x = nodeDepths[node] * horizontalSpacing;
                float y = nodeLanes[node] * verticalSpacing;

                Vector2 newPos = new(x, y);
                node.SetPosition(new Rect(newPos, Vector2.zero));

                // Save the data so it persists across reboots
                node.NodeData.position = newPos;
                EditorUtility.SetDirty(node.NodeData);
            }
        }

        private List<DialogueNodeView> GetParents(DialogueNodeView child)
        {
            List<DialogueNodeView> parents = new();

            if (child.InputPort.connected)
                foreach (Edge edge in child.InputPort.connections)
                    if (edge.output.node is DialogueNodeView parentView)
                        parents.Add(parentView);

            return parents;
        }

        // Helper method to grab all children of a node, regardless of which port they are attached to
        private static List<DialogueNodeView> GetChildren(DialogueNodeView parent)
        {
            List<DialogueNodeView> children = new();

            // Check Default Port
            if (parent.DefaultOutputPort.connected)
                foreach (Edge edge in parent.DefaultOutputPort.connections)
                    if (edge.input.node is DialogueNodeView childView)
                        children.Add(childView);

            // Check Choice Ports
            foreach (Port port in parent.ChoicePorts)
                if (port.connected)
                    foreach (Edge edge in port.connections)
                        if (edge.input.node is DialogueNodeView childView)
                            children.Add(childView);

            return children;
        }

        public void LinkPorts(Port outputPort, Port inputPort)
        {
            // --- NEW: Enforce Port Capacity Rules ---
            // If the output port can only hold one wire, and it already has one, destroy the old one!
            if (outputPort.capacity == Port.Capacity.Single && outputPort.connected)
                // .ToList() creates a temporary copy so we don't break the iterator while deleting
                DeleteElements(outputPort.connections.ToList());

            // (Optional but good practice) Apply the same safety check for the input port
            if (inputPort.capacity == Port.Capacity.Single && inputPort.connected)
                DeleteElements(inputPort.connections.ToList());

            Edge edge = new()
            {
                output = outputPort,
                input = inputPort
            };

            // Connect the data logic
            edge.input.Connect(edge);
            edge.output.Connect(edge);

            // Add the visual wire to the graph
            AddElement(edge);
        }

        private static GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
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

        // --- UPDATED: Make public and return the DialogueNodeView ---
        public DialogueNodeView CreateNewNode(string nodeName, Vector2 position)
        {
            DialogueNode newNodeData = ScriptableObject.CreateInstance<DialogueNode>();
            newNodeData.name = nodeName;
            newNodeData.text = "Enter dialogue here...";

            DialogueNodeView nodeView = new(newNodeData, EdgeListener);
            nodeView.SetPosition(new Rect(position, Vector2.zero));
            nodeView.OnSelectedCallback = OnNodeSelected;

            AddElement(nodeView);

            return nodeView; // Return the node so we can wire it up
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