using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.UI
{
    public class SkillPointsBar : VisualElement
    {
        private readonly Dictionary<EmotionType, Label> emotionLabels;
        private readonly Label genericPointsLabel;

        public SkillPointsBar()
        {
            // --- 1. MAIN CONTAINER: TOP-RIGHT COLUMN ---
            style.position = Position.Absolute;
            style.top = 24;
            style.left = 40; // Anchors the entire element to the top right
            
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.FlexEnd; // Right-aligns both the title and the points row
            // style. = PickingMode.Ignore;

            // --- 2. TITLE LABEL ---
            Label titleLabel = new Label("SKILL POINTS")
            {
                style =
                {
                    color = new StyleColor(new Color(0.65f, 0.65f, 0.65f, 1f)), // Light Gray
                    fontSize = 22,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    letterSpacing = 2,
                    marginBottom = 4 // Space between title and the points row
                }
            };
            Add(titleLabel);

            // --- 3. POINTS ROW ---
            VisualElement pointsRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };
            Add(pointsRow);

            // --- 4. ASSEMBLE POINTS ---
            emotionLabels = new Dictionary<EmotionType, Label>();

            // A) Generic Points (Using null to signify generic)
            VisualElement genericGroup = CreatePointGroup(null, out genericPointsLabel);
            pointsRow.Add(genericGroup);

            // B) Emotion Points dynamically generated
            foreach (EmotionType emotion in Enum.GetValues(typeof(EmotionType)))
            {
                VisualElement emotionGroup = CreatePointGroup(emotion, out Label valueLabel);
                emotionLabels[emotion] = valueLabel;
                pointsRow.Add(emotionGroup);
            }

            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        /// <summary>
        /// Creates a [Number] -> [Icon] grouping to match the reference image.
        /// </summary>
        private VisualElement CreatePointGroup(EmotionType? emotion, out Label valueLabel)
        {
            VisualElement container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginLeft = 16 // Spacing between each point group
                }
            };

            // 1. THE NUMBER VALUE (Comes first, e.g., "2 [Icon]")
            valueLabel = new Label("0")
            {
                style =
                {
                    color = Color.white,
                    fontSize = 20,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 0, marginBottom = 0,
                    marginRight = 6 // Space between the number and the icon
                }
            };
            container.Add(valueLabel);

            // 2. THE ICON
            VisualElement iconElement = new VisualElement
            {
                style =
                {
                    width = 24, 
                    height = 24,
                    alignSelf = Align.Center,
                    unityBackgroundScaleMode = ScaleMode.ScaleToFit
                }
            };

            // Try to load the PNG image for the point type
            Sprite iconSprite = GetIconForPointType(emotion);
            
            if (iconSprite != null)
            {
                iconElement.style.backgroundImage = new StyleBackground(iconSprite);
            }
            else
            {
                // FALLBACK: If no image is found, draw a colored circle so the UI doesn't break
                iconElement.style.backgroundColor = emotion.HasValue ? GetEmotionColor(emotion.Value) : Color.white;
                iconElement.style.borderTopLeftRadius = Length.Percent(50);
                iconElement.style.borderTopRightRadius = Length.Percent(50);
                iconElement.style.borderBottomLeftRadius = Length.Percent(50);
                iconElement.style.borderBottomRightRadius = Length.Percent(50);
            }

            container.Add(iconElement);

            return container;
        }

        /// <summary>
        /// Loads the specific PNG icons dynamically at runtime.
        /// </summary>
        private Sprite GetIconForPointType(EmotionType? emotion)
        {
            // If it's a generic point
            if (!emotion.HasValue) 
                return Resources.Load<Sprite>("Icons/GenericPoint");

            // If it's an emotion point
            return Resources.Load<Sprite>($"Icons/Emotion_{emotion.Value}");
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            SkillTreeManager.OnSkillTreeUpdated += Refresh;
            Refresh(); // Force refresh on load
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            SkillTreeManager.OnSkillTreeUpdated -= Refresh;
        }

        public void Refresh()
        {
            if (SkillTreeManager.Instance == null) return;

            // Update Generic Points
            genericPointsLabel.text = SkillTreeManager.Instance.genericPoints.ToString();

            // Update Emotion Points
            foreach (KeyValuePair<EmotionType, Label> record in emotionLabels)
            {
                if (SkillTreeManager.Instance.emotionPoints.TryGetValue(record.Key, out int currentPoints))
                {
                    // Hide the group completely if points are 0, or keep it visible. 
                    // Depending on your design, you can uncomment the next line to only show emotions you have points for:
                    // record.Value.parent.style.display = currentPoints > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                    
                    record.Value.text = currentPoints.ToString();
                }
            }
        }

        private Color GetEmotionColor(EmotionType emotion)
        {
            return emotion switch
            {
                EmotionType.Red => new Color(0.85f, 0.25f, 0.25f),
                EmotionType.Green => new Color(0.25f, 0.85f, 0.25f),
                EmotionType.Blue => new Color(0.25f, 0.5f, 0.9f),
                EmotionType.Yellow => new Color(0.85f, 0.85f, 0.25f),
                EmotionType.White => Color.white,
                _ => Color.gray
            };
        }
    }
}