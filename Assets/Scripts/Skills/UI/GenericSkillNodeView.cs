using Skills.Skills;
using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.UI
{
    public class GenericSkillNodeView : BaseSkillNodeView
    {
        private readonly GenericNodeData genericData;

        public GenericSkillNodeView(GenericNodeData data, bool isEditor = false) : base(data, isEditor)
        {
            genericData = data;

            // --- 1. GENERIC SPECIFIC STYLING ---
            style.width = 70;
            style.height = 70;
            
            // Perfect Circle
            style.borderTopLeftRadius = Length.Percent(50);
            style.borderTopRightRadius = Length.Percent(50);
            style.borderBottomLeftRadius = Length.Percent(50);
            style.borderBottomRightRadius = Length.Percent(50);

            style.backgroundColor = new StyleColor(new Color(0.88f, 0.88f, 0.88f, 1f));

            titleLabel = new Label(data.NodeName)
            {
                style = 
                { 
                    color = new StyleColor(new Color(0.12f, 0.12f, 0.12f, 1f)), 
                    unityFontStyleAndWeight = FontStyle.Bold,
                    whiteSpace = WhiteSpace.Normal,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            Add(titleLabel);

            RefreshVisualState();
        }

        public override void RefreshVisualState()
        {
            if (isEditorMode || SkillTreeManager.Instance == null) return;

            int currentLevel = SkillTreeManager.Instance.GetNodeLevel(genericData.GUID);
    
            // Dim if locked
            style.opacity = currentLevel > 0 ? 1f : 0.4f;

            // FADE THE BLOOM IN OR OUT
            if (glowElement != null)
            {
                // For generic passives, bloom if they are unlocked
                glowElement.style.opacity = currentLevel > 0 ? 0.8f : 0f; 
            }
        }
    }
}