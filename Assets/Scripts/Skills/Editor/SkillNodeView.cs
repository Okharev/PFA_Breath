using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Skills.Skills; // Required to access BaseNodeData

namespace Skills.Editor
{
    public class SkillNodeView : Node
    {
        public BaseNodeData NodeData;
        public Action<SkillNodeView> OnNodeSelected;

        // Constructor for creating from existing data (Loading)
        public SkillNodeView(BaseNodeData nodeData)
        {
            NodeData = nodeData;
            title = nodeData.NodeName;

            mainContainer.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f, 1f);

            GeneratePorts();
            RefreshVisuals();
        }

        public override void OnSelected()
        {
            base.OnSelected();
            OnNodeSelected?.Invoke(this);
        }

        public void RefreshVisuals()
        {
            // Pattern matching to apply visual styles based on the derived type
            if (NodeData is GenericNodeData)
            {
                titleContainer.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            }
            else if (NodeData is EmotionNodeData emotionData)
            {
                titleContainer.style.backgroundColor = GetEmotionColor(emotionData.RequiredEmotion);
            }
        }

        private static Color GetEmotionColor(EmotionType emotion)
        {
            return emotion switch
            {
                EmotionType.Red => new Color(0.7f, 0.2f, 0.2f, 1f),
                EmotionType.Green => new Color(0.2f, 0.6f, 0.2f, 1f),
                EmotionType.Blue => new Color(0.2f, 0.4f, 0.7f, 1f),
                EmotionType.Yellow => new Color(0.7f, 0.7f, 0.1f, 1f),
                EmotionType.White => new Color(0.8f, 0.8f, 0.8f, 1f),
                _ => new Color(0.25f, 0.25f, 0.25f, 1f)
            };
        }

        private void GeneratePorts()
        {
            Port inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            inputPort.portName = "Requires";
            inputContainer.Add(inputPort);

            Port outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            outputPort.portName = "Unlocks";
            outputContainer.Add(outputPort);

            RefreshExpandedState();
            RefreshPorts();
        }
    }
}