using System;
using Skills.Skills;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Skills.Editor
{
    public class SkillNodeView : Node
    {
        // Define our grid spacing - 25 is the greatest common divisor for center-aligning 150 and 100 width nodes
        private const float GridSnapSize = 25f;
        public BaseNodeData NodeData;
        public Action<SkillNodeView> OnNodeSelected;

        public SkillNodeView(BaseNodeData nodeData)
        {
            NodeData = nodeData;
            title = nodeData.NodeName;

            mainContainer.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f, 1f);

            GeneratePorts();
            RefreshVisuals();
        }

        // --- GRID SNAPPING IMPLEMENTATION ---
        public override void SetPosition(Rect newPos)
        {
            // O(1) mathematical rounding to the nearest grid step (25 units)
            newPos.x = Mathf.Round(newPos.x / GridSnapSize) * GridSnapSize;
            newPos.y = Mathf.Round(newPos.y / GridSnapSize) * GridSnapSize;

            base.SetPosition(newPos);

            // Sync immediately with the underlying data model
            if (NodeData != null)
                // FIX: Assign the entire snapped Rect back to the data model
                NodeData.Position = newPos;
        }

        public override void OnSelected()
        {
            base.OnSelected();
            OnNodeSelected?.Invoke(this);
        }

        public void RefreshVisuals()
        {
            if (NodeData is GenericNodeData)
                titleContainer.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            else if (NodeData is EmotionNodeData emotionData)
                titleContainer.style.backgroundColor = GetEmotionColor(emotionData.RequiredEmotion);
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