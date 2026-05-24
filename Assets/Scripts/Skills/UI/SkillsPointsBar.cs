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
            // Horizontal container assembly using Flexbox rules
            style.flexDirection = FlexDirection.Row;
            style.backgroundColor = new StyleColor(new Color(0.07f, 0.07f, 0.07f, 1f));
            style.paddingTop = 12;
            style.paddingBottom = 12;
            style.paddingLeft = 24;
            style.paddingRight = 24;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;

            // Layout Anchoring: Lock it to the top of the Viewport context safely
            style.position = Position.Absolute;
            style.top = 0;
            style.left = 0;
            style.right = 0;
            style.height = 50;
            style.borderBottomWidth = 2;
            style.borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 1f));

            // Setup Generic Counter Elements
            VisualElement genericGroup = CreatePointGroup("Generic Points", Color.white, out genericPointsLabel);
            Add(genericGroup);

            emotionLabels = new Dictionary<EmotionType, Label>();

            // Generate counters for all existing Emotion types dynamically via code
            foreach (EmotionType emotion in Enum.GetValues(typeof(EmotionType)))
            {
                Color themeColor = GetEmotionColor(emotion);
                VisualElement emotionGroup = CreatePointGroup($"{emotion}", themeColor, out Label valueLabel);
                emotionLabels[emotion] = valueLabel;
                Add(emotionGroup);
            }

            // Hook panel lifecycle registration loops safely to prevent memory leaks
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private VisualElement CreatePointGroup(string title, Color indicatorColor, out Label valueLabel)
        {
            VisualElement container = new();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginRight = 30;

            VisualElement statusDot = new();
            statusDot.style.width = 10;
            statusDot.style.height = 10;
            statusDot.style.borderTopLeftRadius = 5;
            statusDot.style.borderTopRightRadius = 5;
            statusDot.style.borderBottomLeftRadius = 5;
            statusDot.style.borderBottomRightRadius = 5;
            statusDot.style.backgroundColor = indicatorColor;
            statusDot.style.marginRight = 8;
            // Explicitly clamp center self alignment 
            statusDot.style.alignSelf = Align.Center;
            container.Add(statusDot);

            Label titleLabel = new($"{title}: ");
            titleLabel.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.75f));
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            // FIX: Strip default margins skewing alignment
            titleLabel.style.marginTop = 0;
            titleLabel.style.marginBottom = 0;
            container.Add(titleLabel);

            valueLabel = new Label("0");
            valueLabel.style.color = indicatorColor;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            // Strip default margins skewing alignment
            valueLabel.style.marginTop = 0;
            valueLabel.style.marginBottom = 0;
            container.Add(valueLabel);

            return container;
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            SkillTreeManager.OnSkillTreeUpdated += Refresh;
            Refresh();
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            SkillTreeManager.OnSkillTreeUpdated -= Refresh;
        }

        public void Refresh()
        {
            if (SkillTreeManager.Instance == null) return;

            genericPointsLabel.text = SkillTreeManager.Instance.genericPoints.ToString();

            foreach (KeyValuePair<EmotionType, Label> record in emotionLabels)
                if (SkillTreeManager.Instance.emotionPoints.TryGetValue(record.Key, out int currentPoints))
                    record.Value.text = currentPoints.ToString();
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