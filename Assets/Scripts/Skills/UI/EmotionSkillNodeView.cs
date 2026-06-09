using System.Collections.Generic;
using Skills.Skills;
using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.UI
{
    public class EmotionSkillNodeView : BaseSkillNodeView
    {
        private readonly EmotionNodeData emotionData;
        private readonly List<VisualElement> orbitalDots = new();

        // Notice the ": base(data, isEditor)" - This registers all the events from the base class!
        public EmotionSkillNodeView(EmotionNodeData data, bool isEditor = false) : base(data, isEditor)
        {
            emotionData = data;

            // --- EMOTION SPECIFIC STYLING ---
            style.width = 100;
            style.height = 100;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;

            // Perfect Circle
            style.borderTopLeftRadius = Length.Percent(50);
            style.borderTopRightRadius = Length.Percent(50);
            style.borderBottomLeftRadius = Length.Percent(50);
            style.borderBottomRightRadius = Length.Percent(50);

            style.backgroundColor = GetEmotionColor(data.RequiredEmotion);

            // START WITH NO BORDERS (No Glow)
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0;

            // 1. Setup Icon (Assumes CreateIconLayer was added from our previous architecture)
            Sprite iconToUse = data.GrantedAbility.Icon;
            CreateIconLayer(iconToUse);

            // TEXT REMOVED entirely!

            GenerateOrbitalIndicators();
            RefreshVisualState();
        }

        // --- OVERRIDE BASE BEHAVIORS ---

        protected override void OnLongPress()
        {
            // Only Emotion nodes care about Long Presses (Equipping)
            int currentLevel = SkillTreeManager.Instance?.GetNodeLevel(NodeData.GUID) ?? 0;

            if (NodeData is EmotionNodeData emotionNode && emotionNode.UnlocksAbility && currentLevel > 0)
            {
                Debug.Log($"[UI] Long Press Detected! Toggling equip state for {NodeData.NodeName}");
                SkillTreeManager.Instance.ToggleEquipNode(emotionNode);
            }
        }

        public void GenerateOrbitalIndicators()
        {
            // Clear old dots if we are regenerating
            foreach (VisualElement dot in orbitalDots) dot.RemoveFromHierarchy();
            orbitalDots.Clear();

            int maxLevel = emotionData.MaxLevel;

            // REDUCED: The mathematical radius for the orbit. 
            // Previously 95f (which hovered just outside the 150px box).
            // Let's set it to 65f to hover just outside the new 100px box.
            float orbitRadius = 65f;

            // REDUCED: Center of the node. Must be exactly half of the new 100 width!
            float nodeCenter = 50f;

            float startAngle = emotionData.OrbitRotation;
            float span = emotionData.OrbitSpan;
            float endAngle = startAngle + span;

            float angleStep = maxLevel > 1 ? span / (maxLevel - 1) : 0;

            for (int i = 0; i < maxLevel; i++)
            {
                float currentAngle = startAngle + i * angleStep;
                float rad = currentAngle * Mathf.Deg2Rad;

                float targetX = nodeCenter + Mathf.Cos(rad) * orbitRadius;
                float targetY = nodeCenter + Mathf.Sin(rad) * orbitRadius;

                VisualElement dot = new()
                {
                    style =
                    {
                        position = Position.Absolute,
                        // Override parent's Align.Center so it doesn't try to force the dot to the middle
                        alignSelf = Align.FlexStart,
                        width = 16,
                        height = 16,

                        // 1. Set the top-left corner to the target coordinate
                        left = targetX,
                        top = targetY,

                        // 2. Shift the element by -50% of its own size
                        // This effectively centers the dot on the target coordinate
                        translate = new Translate(Length.Percent(-50), Length.Percent(-50)),

                        borderTopLeftRadius = Length.Percent(50),
                        borderTopRightRadius = Length.Percent(50),
                        borderBottomLeftRadius = Length.Percent(50),
                        borderBottomRightRadius = Length.Percent(50),

                        borderBottomWidth = 2,
                        borderTopWidth = 2,
                        borderRightWidth = 2,
                        borderLeftWidth = 2,
                        borderBottomColor = Color.white,
                        borderTopColor = Color.white,
                        borderRightColor = Color.white,
                        borderLeftColor = Color.white,

                        backgroundColor = new Color(0, 0, 0, 0.5f)
                    }
                };

                orbitalDots.Add(dot);
                Add(dot);
            }
        }


        public override void RefreshVisualState()
        {
            if (isEditorMode || SkillTreeManager.Instance == null) return;

            int currentLevel = SkillTreeManager.Instance.GetNodeLevel(emotionData.GUID);
            bool isEquipped = SkillTreeManager.Instance.IsNodeEquipped(emotionData);

            // Dim if completely locked
            style.opacity = currentLevel > 0 ? 1f : 0.4f;

            // FADE THE BLOOM IN OR OUT
            if (glowElement != null)
            {
                glowElement.style.opacity = isEquipped ? 1f : 0f;
            }

            for (int i = 0; i < orbitalDots.Count; i++)
            {
                orbitalDots[i].style.backgroundColor = i < currentLevel ? Color.white : new Color(0, 0, 0, 0.5f);
            }
        }

        private Color GetEmotionColor(EmotionType emotion)
        {
            return emotion switch
            {
                EmotionType.Red => new Color(0.8f, 0.2f, 0.2f),
                EmotionType.Green => new Color(0.2f, 0.8f, 0.2f),
                EmotionType.Blue => new Color(0.1f, 0.6f, 1f),
                EmotionType.Yellow => new Color(0.8f, 0.8f, 0.2f),
                EmotionType.White => Color.white,
                _ => Color.gray
            };
        }
    }
}