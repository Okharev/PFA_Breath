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
        public string GUID;

        public Port InputPort;
        public DialogueNode NodeData;

        // Callback to alert the window that this node was clicked
        public Action<DialogueNodeView> OnSelectedCallback;

        public DialogueNodeView(DialogueNode nodeData)
        {
            NodeData = nodeData;
            GUID = Guid.NewGuid().ToString();

            // --- NEW: Setup Text Preview Label ---
            _textPreviewLabel = new Label();
            _textPreviewLabel.style.whiteSpace = WhiteSpace.Normal; // Allow text to wrap
            _textPreviewLabel.style.maxWidth = 250; // Prevent the node from stretching infinitely
            _textPreviewLabel.style.paddingLeft = 5;
            _textPreviewLabel.style.paddingRight = 5;
            _textPreviewLabel.style.paddingBottom = 5;

            // Add the label to the main container, right below the ports
            mainContainer.Add(_textPreviewLabel);

            GeneratePorts();
            UpdateDefaultPortVisibility();

            // --- NEW: Apply Colors and Text on creation ---
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

        // Add this inside DialogueNodeView.cs
        public void SyncChoicePorts(GraphView graphView)
        {
            foreach (Port port in ChoicePorts)
            {
                if (port.connected) graphView.DeleteElements(port.connections);
                outputContainer.Remove(port);
            }

            ChoicePorts.Clear();

            foreach (DialogueChoice choice in NodeData.choices) AddChoicePort(choice);

            // --- NEW: Toggle the default port on or off based on the new choices ---
            UpdateDefaultPortVisibility(graphView);

            RefreshExpandedState();
            RefreshPorts();
        }

        public void AddChoicePort(DialogueChoice choice)
        {
            Port choicePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single,
                typeof(float));
            choicePort.portName = string.IsNullOrEmpty(choice.choiceText) ? "New Choice" : choice.choiceText;

            // --- NEW: The Magic Bridge ---
            // Store the exact C# memory reference of the choice inside the visual port
            choicePort.userData = choice;

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
            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            InputPort.portName = "Input";
            inputContainer.Add(InputPort);

            DefaultOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single,
                typeof(float));
            DefaultOutputPort.portName = "Next Node";
            outputContainer.Add(DefaultOutputPort);
        }
    }
}