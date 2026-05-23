using System.Collections.Generic;
using Skills.Skills;
using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.UI
{
    public class SkillNodeView : VisualElement
    {
        private readonly bool isEditorMode;
        private readonly Label levelIndicatorLabel;
        private readonly Label titleLabel;
        private bool isLongPressHandled;
        private bool isPulsing;
        private IVisualElementScheduledItem longPressTask;

        private IVisualElementScheduledItem pulseTask;

        public SkillNodeView(BaseNodeData data, bool isEditor = false)
        {
            NodeData = data;
            isEditorMode = isEditor;

            // Positioning
            style.position = Position.Absolute;
            style.left = data.Position.x;
            style.top = data.Position.y;

            // Flexbox Centering
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;

            // Typography & Padding
            style.paddingLeft = style.paddingRight = 8;
            style.borderBottomLeftRadius = style.borderBottomRightRadius = 10;
            style.borderTopLeftRadius = style.borderTopRightRadius = 10;
            style.borderBottomWidth = style.borderTopWidth = style.borderLeftWidth = style.borderRightWidth = 3;

            // UI Polish: Drop Shadow for Depth
            style.textShadow = new TextShadow
                { blurRadius = 2, color = new Color(0, 0, 0, 0.5f), offset = new Vector2(1, 1) };

            // UI Polish: Smooth Scale Transitions (Hardware Accelerated)
            style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> { new("scale") });
            style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new(0.15f) });
            style.transitionTimingFunction =
                new StyleList<EasingFunction>(new List<EasingFunction> { new(EasingMode.EaseOutCubic) });

            titleLabel = new Label(data.NodeName)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2, whiteSpace = WhiteSpace.Normal,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            Add(titleLabel);

            levelIndicatorLabel = new Label("")
            {
                style = { fontSize = 12, unityFontStyleAndWeight = FontStyle.Bold }
            };
            Add(levelIndicatorLabel);

            ApplyTypeSpecificStyling(data);
            RefreshVisualState();

            // Event Listeners
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerOutEvent>(OnPointerOut);
            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);

            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        public BaseNodeData NodeData { get; }

        private void OnAttach(AttachToPanelEvent evt)
        {
            SkillTreeManager.OnSkillTreeUpdated += RefreshVisualState;
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            SkillTreeManager.OnSkillTreeUpdated -= RefreshVisualState;
        }

        private void OnOriginalDetach()
        {
            SkillTreeManager.OnSkillTreeUpdated -= RefreshVisualState;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || isEditorMode) return;

            isLongPressHandled = false;

            // THE FIX: Cache the screen coordinates right now while the event is still alive
            Vector2 capturedPosition = evt.position;

            // Schedule the long-press logic to fire in 500 milliseconds
            longPressTask = schedule.Execute(() =>
            {
                isLongPressHandled = true;
                // Pass the safe, cached variable instead of evt.position
                HandleLongPress(capturedPosition);
            }).StartingIn(500);

            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.button != 0 || isEditorMode) return;

            // If the user let go before 500ms, cancel the hold task
            if (longPressTask != null)
            {
                longPressTask.Pause();
                longPressTask = null;
            }

            // If the long press didn't fire, execute standard Unlock/Click behavior
            if (!isLongPressHandled)
                if (SkillTreeManager.Instance != null && SkillTreeManager.Instance.TryUnlock(NodeData))
                    Debug.Log($"[UI] Successfully leveled up {NodeData.NodeName}");

            evt.StopPropagation();
        }

        // Call this at the end of your RefreshVisualState() method
        private void EvaluatePulseState()
        {
            if (isEditorMode || SkillTreeManager.Instance == null) return;

            bool isUnlocked = SkillTreeManager.Instance.GetNodeLevel(NodeData.GUID) > 0;
            bool canAfford = SkillTreeManager.Instance.CanUnlock(NodeData); // Assuming you have this logic
            bool meetsPrereqs = SkillTreeManager.Instance.MeetsPrerequisites(NodeData);

            // If locked, but we can buy it right now -> Start Pulsing
            if (!isUnlocked && canAfford && meetsPrereqs)
            {
                if (!isPulsing) StartPulseAnimation();
            }
            else
            {
                StopPulseAnimation();
            }
        }

        private void StartPulseAnimation()
        {
            isPulsing = true;
            float pulseTime = 0f;

            // Execute every frame
            pulseTask = schedule.Execute(() =>
            {
                pulseTime += Time.unscaledDeltaTime * 3f; // Speed of the pulse
                // Use a Sine wave to oscillate the scale smoothly between 1.0 and 1.08
                float scale = 1f + (Mathf.Sin(pulseTime) * 0.04f + 0.04f);
                style.scale = new StyleScale(new Vector2(scale, scale));

                // Optionally pulse border brightness
                Color baseColor = Color.yellow;
                baseColor.a = 0.5f + Mathf.Sin(pulseTime) * 0.5f;
                style.borderBottomColor =
                    style.borderTopColor = style.borderLeftColor = style.borderRightColor = baseColor;
            }).Every(16); // ~60fps
        }

        private void StopPulseAnimation()
        {
            if (!isPulsing) return;
            isPulsing = false;
            pulseTask?.Pause();

            // Reset scale when done
            style.scale = new StyleScale(Vector2.one);
        }

        private void OnPointerOut(PointerOutEvent evt)
        {
            // Cancel the press if the user drags their mouse off the node while holding click
            if (longPressTask != null)
            {
                longPressTask.Pause();
                longPressTask = null;
            }
        }


        private void OnPointerEnter(PointerEnterEvent evt)
        {
            // Hover effect: Scale up slightly
            style.scale = new StyleScale(new Vector2(1.05f, 1.05f));
            SkillTooltip.OnUpdateTooltip?.Invoke(NodeData.Description, evt.position);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            // Continuously pass updated coordinates to ensure smooth cursor tracking
            SkillTooltip.OnUpdateTooltip?.Invoke(NodeData.Description, evt.position);
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            // Return to normal
            style.scale = new StyleScale(Vector2.one);
            SkillTooltip.OnHideTooltip?.Invoke();
        }

        private void ApplyTypeSpecificStyling(BaseNodeData data)
        {
            if (data is EmotionNodeData emotionNode)
            {
                style.width = 150;
                style.height = 150;
                style.backgroundColor = GetEmotionColor(emotionNode.RequiredEmotion);
                titleLabel.style.color = Color.white;
                levelIndicatorLabel.style.color = new StyleColor(new Color(0.95f, 0.95f, 0.95f));
            }
            else if (data is GenericNodeData)
            {
                style.width = 100;
                style.height = 100;
                style.backgroundColor = new StyleColor(new Color(0.88f, 0.88f, 0.88f, 1f));
                titleLabel.style.color = new StyleColor(new Color(0.12f, 0.12f, 0.12f, 1f));
                levelIndicatorLabel.style.color = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 1f));
            }
        }

        public void RefreshVisualState()
        {
            if (isEditorMode || SkillTreeManager.Instance == null) return;

            int currentLevel = SkillTreeManager.Instance.GetNodeLevel(NodeData.GUID);
            bool unlocked = currentLevel > 0;
            bool isEquipped = SkillTreeManager.Instance.IsNodeEquipped(NodeData);

            levelIndicatorLabel.text = NodeData is EmotionNodeData emotionNode
                ? $"{currentLevel}/{emotionNode.MaxLevel}"
                : unlocked
                    ? "1/1"
                    : "0/1";

            if (unlocked)
            {
                style.opacity = 1f;
                Color borderColor = isEquipped ? new Color(0.2f, 0.9f, 0.2f, 1f) : new Color(1f, 0.75f, 0f, 1f);
                style.borderBottomColor =
                    style.borderTopColor = style.borderLeftColor = style.borderRightColor = borderColor;
            }
            else
            {
                style.opacity = 0.4f; // Slightly more faded for un-purchased
                Color lockedBorder = NodeData is EmotionNodeData ? Color.white : Color.clear;
                style.borderBottomColor =
                    style.borderTopColor = style.borderLeftColor = style.borderRightColor = lockedBorder;
            }
        }

        private void HandleLongPress(Vector2 screenPosition)
        {
            int currentLevel = SkillTreeManager.Instance?.GetNodeLevel(NodeData.GUID) ?? 0;

            if (NodeData is EmotionNodeData emotionNode && emotionNode.UnlocksAbility && currentLevel > 0)
            {
                Debug.Log($"[UI] Long Press Detected! Toggling equip state for {NodeData.NodeName}");

                // Use the new toggle method to allow equipping and unequipping safely
                SkillTreeManager.Instance.ToggleEquipNode(emotionNode);
            }
        }

        private Color GetEmotionColor(EmotionType emotion)
        {
            return emotion switch
            {
                EmotionType.Red => new Color(0.8f, 0.2f, 0.2f),
                EmotionType.Green => new Color(0.2f, 0.8f, 0.2f),
                EmotionType.Blue => new Color(0.2f, 0.4f, 0.8f),
                EmotionType.Yellow => new Color(0.8f, 0.8f, 0.2f),
                EmotionType.White => Color.white,
                _ => Color.gray
            };
        }

        private void OnClick(ClickEvent evt)
        {
            if (isEditorMode) return;

            if (SkillTreeManager.Instance != null && SkillTreeManager.Instance.TryUnlock(NodeData))
                Debug.Log($"[UI] Successfully leveled up {NodeData.NodeName}");
            evt.StopPropagation();
        }
    }
}