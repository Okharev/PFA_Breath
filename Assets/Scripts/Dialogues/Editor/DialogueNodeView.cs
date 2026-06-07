using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dialogues.Editor
{
    public class DialogueNodeView : Node
    {
        private readonly Label _textPreviewLabel;
        public List<Port> ChoicePorts = new();
        public Port DefaultOutputPort;

        // --- Hold a reference to the listener ---
        public DialogueEdgeListener EdgeListener;
        public string GUID;

        public Port InputPort;
        public DialogueNode NodeData;

        // Callback to alert the window that this node was clicked
        public Action<DialogueNodeView> OnSelectedCallback;

        public DialogueNodeView(DialogueNode nodeData, DialogueEdgeListener edgeListener)
        {
            NodeData = nodeData;
            EdgeListener = edgeListener; // Save it!
            GUID = Guid.NewGuid().ToString();

            // --- Setup Text Preview Label ---
            _textPreviewLabel = new Label();
            _textPreviewLabel.style.whiteSpace = WhiteSpace.Normal; // Allow text to wrap
            _textPreviewLabel.style.maxWidth = 250; // Prevent the node from stretching infinitely
            _textPreviewLabel.style.paddingLeft = 5;
            _textPreviewLabel.style.paddingRight = 5;
            _textPreviewLabel.style.paddingBottom = 5;

            // Add the label to the main container, right below the ports
            mainContainer.Add(_textPreviewLabel);

            // 1. Build the Input and Default Next ports
            GeneratePorts();

            // ==========================================
            // --- THE FIX: Build Choice Ports on Load ---
            // ==========================================
            if (NodeData.choices != null)
                foreach (DialogueChoice choice in NodeData.choices)
                    AddChoicePort(choice);

            // 2. Now that Choice ports exist, this logic will correctly hide the default port!
            UpdateDefaultPortVisibility();

            // --- Apply Colors and Text on creation ---
            RefreshVisuals();

            RefreshExpandedState();
            RefreshPorts();
        }

        public void RefreshVisuals()
        {
            // 1. Update Title and Background Color
            if (NodeData.speaker != null)
            {
                title = NodeData.speaker.speakerName;
                // Target the built-in titleContainer of the GraphView Node
                titleContainer.style.backgroundColor = NodeData.speaker.speakerColor;
            }
            else
            {
                title = "No Speaker";
                titleContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            }

            // 2. Update Text Preview (Truncate if it's too long)
            string preview = string.IsNullOrEmpty(NodeData.text) ? "..." : NodeData.text;
            if (preview.Length > 60) preview = preview.Substring(0, 57) + "...";
            _textPreviewLabel.text = preview;
        }

        public void SyncChoicePorts(GraphView graphView)
        {
            // 1. Remember existing connections before we wipe the ports
            Dictionary<int, DialogueNodeView> savedConnections = new();

            for (int i = 0; i < ChoicePorts.Count; i++)
            {
                Port port = ChoicePorts[i];
                if (port.connected)
                {
                    // Look at what this port is currently wired to and save it
                    foreach (Edge edge in port.connections)
                        if (edge.input.node is DialogueNodeView targetNode)
                        {
                            savedConnections[i] = targetNode;
                            break;
                        }

                    // Safely delete the old visual wire
                    graphView.DeleteElements(port.connections);
                }

                outputContainer.Remove(port);
            }

            ChoicePorts.Clear();

            // 2. Rebuild the ports from the updated data
            for (int i = 0; i < NodeData.choices.Count; i++)
            {
                DialogueChoice choice = NodeData.choices[i];
                AddChoicePort(choice);

                // 3. Restore the visual wires if they existed!
                if (savedConnections.TryGetValue(i, out DialogueNodeView targetNode))
                    // Cast the graph to our custom graph so we can access LinkPorts
                    if (graphView is DialogueGraphView dialogueGraph)
                        dialogueGraph.LinkPorts(ChoicePorts[i], targetNode.InputPort);
            }

            UpdateDefaultPortVisibility(graphView);

            RefreshExpandedState();
            RefreshPorts();
        }


        public void AddChoicePort(DialogueChoice choice)
        {
            // Replace InstantiatePort with Port.Create<Edge>
            Port choicePort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single,
                typeof(float));
            choicePort.portName = string.IsNullOrEmpty(choice.choiceText) ? "New Choice" : choice.choiceText;
            choicePort.userData = choice;

            choicePort.AddManipulator(new EdgeConnector<Edge>(EdgeListener)); // <-- The Magic Hook

            ChoicePorts.Add(choicePort);
            outputContainer.Add(choicePort);
        }

        // Add this inside DialogueNodeView.cs
        public void UpdateDefaultPortVisibility(GraphView graphView = null)
        {
            if (NodeData.choices != null && NodeData.choices.Count > 0)
            {
                // 1. We have choices. Hide the default port.
                DefaultOutputPort.style.display = DisplayStyle.None;

                // 2. Safely destroy any wires that were attached to the default port before hiding it
                if (DefaultOutputPort.connected && graphView != null)
                {
                    graphView.DeleteElements(DefaultOutputPort.connections);
                    NodeData.nextNode = null; // Clean up the underlying game data
                    EditorUtility.SetDirty(NodeData);
                }
            }
            else
            {
                // 1. No choices. Show the default port.
                DefaultOutputPort.style.display = DisplayStyle.Flex;
            }
        }


        // --- NEW: Trigger selection event ---
        public override void OnSelected()
        {
            base.OnSelected();

            // Tell the Editor Window to show this node's data in the side panel
            OnSelectedCallback?.Invoke(this);
        }

        // Clear the side panel when deselected (optional but recommended)
        public override void OnUnselected()
        {
            base.OnUnselected();
            OnSelectedCallback?.Invoke(null);
        }

        private void GeneratePorts()
        {
            // Replace InstantiatePort with Port.Create<Edge> and add our custom manipulator
            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            InputPort.portName = "Input";
            InputPort.AddManipulator(new EdgeConnector<Edge>(EdgeListener)); // <-- The Magic Hook
            inputContainer.Add(InputPort);

            DefaultOutputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single,
                typeof(float));
            DefaultOutputPort.portName = "Next Node";
            DefaultOutputPort.AddManipulator(new EdgeConnector<Edge>(EdgeListener)); // <-- The Magic Hook
            outputContainer.Add(DefaultOutputPort);
        }
    }
}