using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Skills.Editor
{
    public class SkillNodeView : Node
    {
        public SkillNodeData NodeData;
        public Action<SkillNodeView> OnNodeSelected;

        public SkillNodeView(string nodeName)
        {
            title = nodeName;

            NodeData = new SkillNodeData
            {
                GUID = Guid.NewGuid().ToString(),
                NodeName = nodeName
            };

            mainContainer.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f, 1f);

            GeneratePorts();
            RefreshVisuals(); // Apply color on creation
        }

        public override void OnSelected()
        {
            base.OnSelected();
            OnNodeSelected?.Invoke(this);
        }

        // --- NEW: Dynamic Coloring Logic ---
        public void RefreshVisuals()
        {
            if (NodeData.NodeType == NodeType.Generic)
                // Default dark grey for Generic nodes
                titleContainer.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            else
                // Apply specific colors for Emotion nodes
                titleContainer.style.backgroundColor = GetEmotionColor(NodeData.Cost.RequiredEmotion);
        }

        private Color GetEmotionColor(EmotionType emotion)
        {
            return emotion switch
            {
                EmotionType.Red => new Color(0.7f, 0.2f, 0.2f, 1f),
                EmotionType.Green => new Color(0.2f, 0.6f, 0.2f, 1f),
                EmotionType.Blue => new Color(0.2f, 0.4f, 0.7f, 1f),
                EmotionType.Yellow => new Color(0.7f, 0.7f, 0.1f, 1f),
                EmotionType.White => new Color(0.8f, 0.8f, 0.8f, 1f), // Off-white to keep text readable
                _ => new Color(0.25f, 0.25f, 0.25f, 1f)
            };
        }

        private void GeneratePorts()
        {
            Port inputPort =
                InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            inputPort.portName = "Requires";
            inputContainer.Add(inputPort);

            Port outputPort =
                InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            outputPort.portName = "Unlocks";
            outputContainer.Add(outputPort);

            RefreshExpandedState();
            RefreshPorts();
        }
    }
}