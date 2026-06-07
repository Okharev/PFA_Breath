using System;
using Skills.Skills;
using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.UI
{
    public class SkillTreeCanvas : VisualElement
    {
        private SkillTreeGraph graphData;

        public SkillTreeCanvas()
        {
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void OnAttach(AttachToPanelEvent evt) => SkillTreeManager.OnSkillTreeUpdated += MarkDirtyRepaint;
        private void OnDetach(DetachFromPanelEvent evt) => SkillTreeManager.OnSkillTreeUpdated -= MarkDirtyRepaint;

        public void Populate(SkillTreeGraph graph, bool isEditor = false)
        {
            graphData = graph;
            graphData.InitializeRuntimeLookup();
            Clear();
            foreach (BaseNodeData node in graphData.AllNodes)
            {
                VisualElement nodeView = SkillNodeFactory.CreateNodeView(node, isEditor);
                if (nodeView != null) Add(nodeView);
            }
            MarkDirtyRepaint();
        }

        
        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (graphData is null) return;

            Painter2D paint2D = context.painter2D;
            paint2D.lineCap = LineCap.Round;
            paint2D.lineJoin = LineJoin.Round;

            bool isEditorPreview = !Application.isPlaying;

            foreach (BaseNodeData node in graphData.AllNodes)
            {
                Vector2 centerA = GetCenterPosition(node);
                float radiusA = GetNodeDiameter(node) * 0.5f;
        
                bool isTargetUnlocked = isEditorPreview || (SkillTreeManager.Instance != null && SkillTreeManager.Instance.GetNodeLevel(node.GUID) > 0);

                foreach (string reqGuid in node.PrerequisiteGUIDs)
                {
                    BaseNodeData sourceNode = graphData.GetNodeByGUID(reqGuid);
                    if (sourceNode == null) continue;

                    Vector2 centerB = GetCenterPosition(sourceNode); // Note: Swapping to match direction logic
                    float radiusB = GetNodeDiameter(sourceNode) * 0.5f;

                    // 1. Calculate Edge-to-Edge Points
                    Vector2 direction = (centerA - centerB).normalized; 
                    Vector2 startPoint = centerB + (direction * radiusB);
                    Vector2 endPoint = centerA - (direction * radiusA);

                    bool isSourceUnlocked = isEditorPreview || (SkillTreeManager.Instance != null && SkillTreeManager.Instance.GetNodeLevel(sourceNode.GUID) > 0);
                    bool fullyUnlocked = isTargetUnlocked && isSourceUnlocked;

                    // 2. Draw using these precise boundary coordinates
                    DrawConnection(paint2D, startPoint, endPoint, sourceNode, node, fullyUnlocked);
                }
            }
        }

        private void DrawConnection(Painter2D paint2D, Vector2 start, Vector2 end, BaseNodeData sourceNode, BaseNodeData targetNode, bool unlocked)
        {
            // 1. Determine Thickness
            bool isSourceEmotion = sourceNode is EmotionNodeData;
            bool isTargetEmotion = targetNode is EmotionNodeData;
            float thickness = (isSourceEmotion && isTargetEmotion) ? 16f : 4f;

            // 2. Determine Colors
            Color startColor = GetNodeColor(sourceNode);
            Color endColor = GetNodeColor(targetNode);

            // If locked, dim the colors
            if (!unlocked)
            {
                startColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                endColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }

            // 3. Create the Gradient properly
            Gradient gradient = new Gradient();
            gradient.colorKeys = new GradientColorKey[] 
            { 
                new GradientColorKey(startColor, 0.0f), 
                new GradientColorKey(endColor, 1.0f) 
            };
            gradient.alphaKeys = new GradientAlphaKey[] 
            { 
                new GradientAlphaKey(startColor.a, 0.0f), 
                new GradientAlphaKey(endColor.a, 1.0f) 
            };

            // 4. Draw Line
            paint2D.lineWidth = thickness;
            paint2D.strokeGradient = gradient; // Correctly assigns the UnityEngine.Gradient
    
            paint2D.BeginPath();
            paint2D.MoveTo(start);
            paint2D.LineTo(end);
            paint2D.Stroke();
        }

        private Color GetNodeColor(BaseNodeData node)
        {
            if (node is EmotionNodeData eNode)
            {
                return eNode.RequiredEmotion switch
                {
                    EmotionType.Red => new Color(0.8f, 0.2f, 0.2f),
                    EmotionType.Green => new Color(0.2f, 0.8f, 0.2f),
                    EmotionType.Blue => new Color(0.1f, 0.6f, 1f),
                    EmotionType.Yellow => new Color(0.8f, 0.8f, 0.2f),
                    EmotionType.White => Color.white,
                    _ => Color.gray
                };
            }
            // Default color for Generic nodes
            return new Color(0.88f, 0.88f, 0.88f, 1f);
        }

        private static float GetNodeDiameter(BaseNodeData node)
        {

            return node is EmotionNodeData ? 100f : 70f;
        }

        private static Vector2 GetCenterPosition(BaseNodeData node)
        {
            // The offset must ALWAYS be exactly half of the diameter above!
            // Reduced from 75f (half of 150) to 50f (half of 100)
            // Reduced from 50f (half of 100) to 35f (half of 70)
            float offset = (node is EmotionNodeData) ? 50f : 35f;
            return new Vector2(node.Position.x + offset, node.Position.y + offset);
        }
    }
}