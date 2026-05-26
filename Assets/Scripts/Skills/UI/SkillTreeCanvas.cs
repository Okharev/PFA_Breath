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

            // Register event listeners to repaint paths on currency/state changes
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            SkillTreeManager.OnSkillTreeUpdated += MarkDirtyRepaint;
        }

        private void OnOriginalDetach()
        {
            SkillTreeManager.OnSkillTreeUpdated -= MarkDirtyRepaint;
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            OnOriginalDetach();
        }

        public void Populate(SkillTreeGraph graph, bool isEditor = false)
        {
            graphData = graph;
            graphData.InitializeRuntimeLookup();
            Clear();

            foreach (BaseNodeData node in graphData.AllNodes)
            {
                SkillNodeView nodeView = new(node, isEditor);
                Add(nodeView);
            }

            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (graphData is null) return;

            Painter2D paint2D = context.painter2D;
            paint2D.lineWidth = 4f;
            paint2D.lineCap = LineCap.Round;
            paint2D.lineJoin = LineJoin.Round;

            Color lockedColor = new(0.3f, 0.3f, 0.3f, 0.5f);
            Color unlockedPathColor = new(0f, 0.85f, 1f, 1f); 

            // Determine if we are in Editor Preview mode (not playing)
            bool isEditorPreview = !Application.isPlaying;

            foreach (BaseNodeData node in graphData.AllNodes)
            {
                Vector2 targetPos = GetCenterPosition(node);
        
                // If in editor preview, simulate it as unlocked so we can see the pretty colored lines
                bool isTargetUnlocked = isEditorPreview || (SkillTreeManager.Instance != null && SkillTreeManager.Instance.GetNodeLevel(node.GUID) > 0);

                foreach (string reqGuid in node.PrerequisiteGUIDs)
                {
                    BaseNodeData sourceNode = graphData.GetNodeByGUID(reqGuid);
                    if (sourceNode != null)
                    {
                        Vector2 sourcePos = GetCenterPosition(sourceNode);
                        bool isSourceUnlocked = isEditorPreview || (SkillTreeManager.Instance != null && SkillTreeManager.Instance.GetNodeLevel(sourceNode.GUID) > 0);

                        // Draw Logic
                        paint2D.strokeColor = isTargetUnlocked && isSourceUnlocked ? unlockedPathColor : lockedColor;

                        paint2D.BeginPath();
                        paint2D.MoveTo(sourcePos);

                        float distanceX = Mathf.Abs(targetPos.x - sourcePos.x);
                        float distanceY = Mathf.Abs(targetPos.y - sourcePos.y);

                        Vector2 cp1, cp2;
                        if (distanceY > distanceX)
                        {
                            // Vertical flow
                            cp1 = new Vector2(sourcePos.x, sourcePos.y + (targetPos.y - sourcePos.y) * 0.5f);
                            cp2 = new Vector2(targetPos.x, sourcePos.y + (targetPos.y - sourcePos.y) * 0.5f);
                        }
                        else
                        {
                            // Horizontal flow
                            cp1 = new Vector2(sourcePos.x + (targetPos.x - sourcePos.x) * 0.5f, sourcePos.y);
                            cp2 = new Vector2(sourcePos.x + (targetPos.x - sourcePos.x) * 0.5f, targetPos.y);
                        }

                        paint2D.BezierCurveTo(cp1, cp2, targetPos);
                        paint2D.Stroke();
                    }
                }
            }
        }

        private static Vector2 GetCenterPosition(BaseNodeData node)
        {
            float x = node.Position.x;
            float y = node.Position.y;

            // ALIGNMENT MATRIX CORRECTION: Matches the updated UI dimensions perfectly
            if (node is EmotionNodeData) return new Vector2(x + 75f, y + 75f); // Half of 150x150 boundary canvas plane

            return new Vector2(x + 50f, y + 50f); // Half of 100x100 boundary canvas plane
        }
    }
}