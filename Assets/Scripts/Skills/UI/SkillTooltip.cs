using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.UI
{
    public class SkillTooltip : VisualElement
    {
        // GLOBAL EVENT BUS FOR DYNAMIC TRACKING
        public static Action<string, Vector2> OnUpdateTooltip;
        public static Action OnHideTooltip;

        private readonly Label descriptionLabel;
        private IVisualElementScheduledItem delayTask;
        private bool isTimerRunning;
        private string pendingDescription;

        public SkillTooltip()
        {
            pickingMode = PickingMode.Ignore;

            // Layout Styling Configuration
            style.position = Position.Absolute;
            style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.05f, 0.98f));
            style.paddingTop = 10;
            style.paddingBottom = 10;
            style.paddingLeft = 14;
            style.paddingRight = 14;

            style.borderBottomWidth = 1;
            style.borderTopWidth = 1;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            style.borderBottomColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
            style.borderTopColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
            style.borderLeftColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
            style.borderRightColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));

            style.borderBottomLeftRadius = 6;
            style.borderBottomRightRadius = 6;
            style.borderTopLeftRadius = 6;
            style.borderTopRightRadius = 6;
            style.maxWidth = 280;

            descriptionLabel = new Label();
            descriptionLabel.style.color = Color.white;
            descriptionLabel.style.fontSize = 12;
            descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            Add(descriptionLabel);

            // NATIVE TRANSITION CONFIGURATION (Unity 6 Compliant)
            // Register an interpolation curve on the opacity style parameter
            style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> { "opacity" });
            style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new(0.3f, TimeUnit.Second) });

            // Set initial state to completely transparent and hidden
            style.opacity = 0f;
            style.display = DisplayStyle.None;

            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            OnUpdateTooltip += HandleTooltipUpdate;
            OnHideTooltip += Hide;
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            OnUpdateTooltip -= HandleTooltipUpdate;
            OnHideTooltip -= Hide;
        }

        private void HandleTooltipUpdate(string description, Vector2 screenPosition)
        {
            if (string.IsNullOrEmpty(description)) return;

            // 1. Continuously update positions so it follows the mouse layout plane smoothly
            const float offsetX = 18f;
            const float offsetY = 18f;
            style.left = screenPosition.x + offsetX;
            style.top = screenPosition.y + offsetY;

            // 2. If the timer isn't running yet, schedule the 2-second fade-in execution window
            if (!isTimerRunning && style.opacity.value < 1f)
            {
                pendingDescription = description;
                isTimerRunning = true;

                // Make layout calculation ready but keep it invisible
                style.display = DisplayStyle.Flex;
                style.opacity = 0f;

                // Low-overhead background worker scheduling
                delayTask = schedule.Execute(ExecuteDisplay).StartingIn(1000);
            }
        }

        private void ExecuteDisplay()
        {
            descriptionLabel.text = pendingDescription;
            // Changing this value kicks off the native hardware opacity blending transition
            style.opacity = 1f;
        }

        private void Hide()
        {
            // Cancel any pending scheduled tasks if the cursor exits the node early
            if (delayTask != null)
            {
                delayTask.Pause();
                delayTask = null;
            }

            isTimerRunning = false;
            style.opacity = 0f;
            style.display = DisplayStyle.None;
        }
    }
}