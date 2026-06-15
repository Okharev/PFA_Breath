using System;
using System.Collections.Generic;
using Skills.Skills; // Required to access BaseNodeData
using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.UI
{
    public class SkillTooltip : VisualElement
    {
        // GLOBAL EVENT BUS - Now passes the entire node data!
        public static Action<BaseNodeData, Vector2> OnUpdateTooltip;
        public static Action OnHideTooltip;

        // UI Elements
        private readonly Label typeLabel;
        private readonly Label nameLabel;
        private readonly VisualElement badgeElement;
        private readonly Label badgeLabel;
        private readonly Label descriptionLabel;

        // State Tracking
        private IVisualElementScheduledItem delayTask;
        private bool isTimerRunning;
        private BaseNodeData pendingNode;

        public SkillTooltip()
        {
            pickingMode = PickingMode.Ignore;

            // --- MAIN CONTAINER STYLING ---
            style.position = Position.Absolute;
            style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.05f, 0.98f));
            style.paddingTop = 12; style.paddingBottom = 12;
            style.paddingLeft = 16; style.paddingRight = 16;
            style.borderBottomWidth = 1; style.borderTopWidth = 1;
            style.borderLeftWidth = 1; style.borderRightWidth = 1;
            style.borderBottomColor = style.borderTopColor = style.borderLeftColor = style.borderRightColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f, 1f));
            style.borderBottomLeftRadius = 6; style.borderBottomRightRadius = 6;
            style.borderTopLeftRadius = 6; style.borderTopRightRadius = 6;
            style.width = 280; // Fixed width for consistent paragraph wrapping

            // --- HEADER ROW (Flex Direction: Row) ---
            VisualElement headerRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    alignItems = Align.Center
                }
            };

            // Left Column: Type and Name
            VisualElement titleCol = new VisualElement { style = { flexDirection = FlexDirection.Column } };
            
            typeLabel = new Label
            {
                style =
                {
                    color = new Color(0.8f, 0.8f, 0.8f), // Light gray
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    letterSpacing = 1
                }
            };

            nameLabel = new Label
            {
                style =
                {
                    color = Color.white,
                    fontSize = 16,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 2
                }
            };
            
            titleCol.Add(typeLabel);
            titleCol.Add(nameLabel);

            // Right Column: The Circular Badge
            badgeElement = new VisualElement
            {
                style =
                {
                    width = 24, height = 24,
                    backgroundColor = Color.white,
                    borderTopLeftRadius = Length.Percent(50), borderTopRightRadius = Length.Percent(50),
                    borderBottomLeftRadius = Length.Percent(50), borderBottomRightRadius = Length.Percent(50),
                    alignItems = Align.Center, justifyContent = Justify.Center
                }
            };

            badgeLabel = new Label
            {
                style =
                {
                    color = Color.black,
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            badgeElement.Add(badgeLabel);

            headerRow.Add(titleCol);
            headerRow.Add(badgeElement);

            // --- DIVIDER LINE ---
            VisualElement divider = new VisualElement
            {
                style =
                {
                    height = 2,
                    backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f),
                    marginTop = 8, marginBottom = 8
                }
            };

            // --- DESCRIPTION ---
            descriptionLabel = new Label
            {
                style =
                {
                    color = new Color(0.85f, 0.85f, 0.85f),
                    fontSize = 13,
                    whiteSpace = WhiteSpace.Normal // Allows text wrapping
                }
            };

            // Assemble Hierarchy
            Add(headerRow);
            Add(divider);
            Add(descriptionLabel);

            // Transitions
            style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> { "opacity" });
            style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new(0.2f, TimeUnit.Second) });
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

        private void HandleTooltipUpdate(BaseNodeData nodeData, Vector2 screenPosition)
        {
            if (nodeData == null) return;

            // Follow mouse position
            const float offsetX = 18f;
            const float offsetY = 18f;
            style.left = screenPosition.x + offsetX;
            style.top = screenPosition.y + offsetY;

            if (!isTimerRunning && style.opacity.value < 1f)
            {
                pendingNode = nodeData;
                isTimerRunning = true;
                style.display = DisplayStyle.Flex;
                style.opacity = 0f;

                // Fire quickly to feel responsive
                delayTask = schedule.Execute(ExecuteDisplay).StartingIn(300);
            }
        }

private void ExecuteDisplay()
{
    // O(1) Type Pattern Matching to populate the UI
    if (pendingNode is EmotionNodeData emotionNode)
    {
        typeLabel.text = emotionNode.UnlocksAbility ? "ACTIVE ABILITY" : "PASSIVE UPGRADE";
        nameLabel.text = emotionNode.NodeName.ToUpper();
        
        // Set the badge to show Emotion Cost
        badgeLabel.text = emotionNode.BaseEmotionCost.ToString();
        badgeElement.style.display = DisplayStyle.Flex;

        // 1. Start with the Skill Node's base flavor/lore description
        string fullDescription = emotionNode.Description;

        // 2. Dynamically append Ability-specific mechanics if it grants one
        if (emotionNode.GrantedAbility != null)
        {
            // Append the Ability's unique description in italics
            if (!string.IsNullOrEmpty(emotionNode.GrantedAbility.description))
            {
                fullDescription += $"\n\n<i>{emotionNode.GrantedAbility.description}</i>";
            }

            // Append a formatted tactical data block
            fullDescription += "\n\n<b><color=#FFD700>TACTICAL DATA:</color></b>";
            fullDescription += $"\n• Turn Cost: {emotionNode.GrantedAbility.turnCost}";
            
            if (emotionNode.GrantedAbility.cooldownTurns > 0)
                fullDescription += $"\n• Cooldown: {emotionNode.GrantedAbility.cooldownTurns} Turns";
                
            if (emotionNode.GrantedAbility.channelTurns > 0)
                fullDescription += $"\n• Channel Time: {emotionNode.GrantedAbility.channelTurns} Turns";
        }

        descriptionLabel.text = fullDescription;
    }
    else if (pendingNode is GenericNodeData genericNode)
    {
        typeLabel.text = "PASSIVE STATS";
        nameLabel.text = genericNode.NodeName.ToUpper();
        
        // Show generic point cost in the badge
        badgeLabel.text = genericNode.GenericCost.ToString();
        badgeElement.style.display = DisplayStyle.Flex;
        
        // 1. Start with the Generic Node's description
        string fullDescription = genericNode.Description;

        // 2. (Optional) You can dynamically read the granted stats here in the future
        if (genericNode.GrantedStats != null && genericNode.GrantedStats.Count > 0)
        {
            fullDescription += "\n\n<b><color=#42f5aa>GRANTED STATS:</color></b>";
            foreach (var statMod in genericNode.GrantedStats)
            {
                string sign = statMod.Value >= 0 ? "+" : "";
                string percent = statMod.Type == ModifierType.Flat ? "" : "%";
                fullDescription += $"\n• {statMod.Stat}: {sign}{statMod.Value}{percent}";
            }
        }

        descriptionLabel.text = fullDescription;
    }

    style.opacity = 1f;
}

        private void Hide()
        {
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