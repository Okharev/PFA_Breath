using System;
using Skills;
using Skills.Skills;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class SkillEquipOverlay : VisualElement
    {
        public static Action<EmotionNodeData, Vector2> OnShowEquipMenu;
        public static Action OnHideEquipMenu;

        private EmotionNodeData targetNode;
        private readonly Button equipButton;

        public SkillEquipOverlay()
        {
            style.position = Position.Absolute;
            style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.98f));
            style.borderBottomWidth = 2;
            style.borderTopWidth = 2;
            style.borderLeftWidth = 2;
            style.borderRightWidth = 2;
            style.borderBottomColor = new StyleColor(new Color(1f, 0.75f, 0f, 1f)); // Gold border
            style.borderTopColor = new StyleColor(new Color(1f, 0.75f, 0f, 1f));
            style.borderLeftColor = new StyleColor(new Color(1f, 0.75f, 0f, 1f));
            style.borderRightColor = new StyleColor(new Color(1f, 0.75f, 0f, 1f));
            style.borderBottomLeftRadius = 5;
            style.borderBottomRightRadius = 5;
            style.borderTopLeftRadius = 5;
            style.borderTopRightRadius = 5;
            style.paddingTop = 10;
            style.paddingBottom = 10;
            style.paddingLeft = 10;
            style.paddingRight = 10;

            equipButton = new Button(HandleEquipClicked);
            equipButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.6f, 0.2f, 1f)); // Green
            equipButton.style.color = Color.white;
            equipButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(equipButton);

            style.display = DisplayStyle.None;

            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
            RegisterCallback<PointerLeaveEvent>(_ => Hide()); // Auto-hide if mouse leaves the menu
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            OnShowEquipMenu += Show;
            OnHideEquipMenu += Hide;
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            OnShowEquipMenu -= Show;
            OnHideEquipMenu -= Hide;
        }

        private void Show(EmotionNodeData node, Vector2 screenPos)
        {
            targetNode = node;
            equipButton.text = $"Equip to [{node.IntendedSlot}]";
            
            style.left = screenPos.x + 10;
            style.top = screenPos.y - 10;
            style.display = DisplayStyle.Flex;
        }

        private void Hide()
        {
            style.display = DisplayStyle.None;
            targetNode = null;
        }

        private void HandleEquipClicked()
        {
            if (targetNode == null) return;

            // Find the player's ability controller
            var playerController = UnityEngine.Object.FindAnyObjectByType<Ability.PlayerAbilityController>();
            if (playerController != null)
            {
                int nodeLevel = SkillTreeManager.Instance.GetNodeLevel(targetNode.GUID);
                playerController.EquipAbility(targetNode.GrantedAbilityId, targetNode.IntendedSlot, nodeLevel);
            }

            Hide(); // Close menu after equipping
        }
    }
}