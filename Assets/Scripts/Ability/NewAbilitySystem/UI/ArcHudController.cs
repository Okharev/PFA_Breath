using System.Collections.Generic;
using Dialogues;
using Skills;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ability.NewAbilitySystem.UI
{
    public class ArcHudController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private AmmoComponent _ammoComponent;

        // Internal cache
        private ArcHudPanel _arcHud;
        private Dictionary<AbilitySlot, AbilityData> _trackedLoadout = new();

        private void HandleDialogueStarted(Conversation c)
        {
            if (_arcHud != null)
            {
                // DisplayStyle.None removes the element from the layout engine entirely
                _arcHud.style.display = DisplayStyle.None;
            }
        }

        private void HandleDialogueEnded()
        {
            if (_arcHud != null)
            {
                // DisplayStyle.Flex returns it to the layout engine
                _arcHud.style.display = DisplayStyle.Flex;
            }
        }
        
        // 2. ADD THIS: Bind to the global singleton in Start
        private void Start()
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.OnConversationStarted += HandleDialogueStarted;
                DialogueManager.Instance.OnConversationEnded += HandleDialogueEnded;
            }
        }

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            _arcHud = _uiDocument.rootVisualElement.Q<ArcHudPanel>();
            if (_arcHud == null) return;

            var skipButton = _arcHud.GetSkipTurnButton();
            if (skipButton != null)
            {
                skipButton.clicked += HandleSkipTurnClicked;
            }

            if (_ammoComponent != null)
            {
                _ammoComponent.OnAmmoChanged += HandleAmmoChanged;
                HandleAmmoChanged(_ammoComponent.CurrentAmmo, _ammoComponent.maxAmmo);
            }

            if (_playerController != null)
            {
                _playerController.OnLoadoutChanged += HandleLoadoutChanged;
                _playerController.OnActiveSlotChanged += HandleActiveSlotChanged; 
            
                _arcHud.GetSlot(MapSlotToIndex(AbilitySlot.Primary))?.SetHotkey("1");
                _arcHud.GetSlot(MapSlotToIndex(AbilitySlot.Secondary))?.SetHotkey("2");
                _arcHud.GetSlot(MapSlotToIndex(AbilitySlot.Dash))?.SetHotkey("SHF");
                _arcHud.GetSlot(MapSlotToIndex(AbilitySlot.Special))?.SetHotkey("R");
            }
        }

        // 3. ADD THIS: Prevent memory leaks by unsubscribing
        private void OnDestroy() 
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.OnConversationStarted -= HandleDialogueStarted;
                DialogueManager.Instance.OnConversationEnded -= HandleDialogueEnded;
            }
        }
        
        private static void HandleSkipTurnClicked()
        {
            // Observer Check: Ensure we are actually in Combat and a turn isn't currently running
            if (GameModeManager.Instance.CurrentMode == GameMode.Combat && !TurnManager.Instance.IsExecuting)
            {
                // Progress the turn manager by 1 tick
                TurnManager.Instance.RequestTurns(1);
            }
        }

        private void OnDisable()
        {
            // --- Unbind Skip Button ---
            var skipButton = _arcHud?.GetSkipTurnButton();
            if (skipButton != null)
            {
                skipButton.clicked -= HandleSkipTurnClicked;
            }
            
            // Prevent memory leaks when the scene unloads or object is destroyed
            if (_ammoComponent != null)
                _ammoComponent.OnAmmoChanged -= HandleAmmoChanged;

            if (_playerController != null)
            {
                _playerController.OnLoadoutChanged -= HandleLoadoutChanged;
                _playerController.OnActiveSlotChanged -= HandleActiveSlotChanged; // Prevent memory leak
            }
        }
        
        private void HandleActiveSlotChanged(AbilitySlot activeSlot)
        {
            var primarySlot = _arcHud?.GetSlot(MapSlotToIndex(AbilitySlot.Primary));
            var secondarySlot = _arcHud?.GetSlot(MapSlotToIndex(AbilitySlot.Secondary));

            if (primarySlot == null || secondarySlot == null) return;

            // Use standard .selected highlighting rather than physics/stacking overrides
            if (activeSlot == AbilitySlot.Primary)
            {
                primarySlot.AddToClassList("selected");
                secondarySlot.RemoveFromClassList("selected");
            }
            else 
            {
                secondarySlot.AddToClassList("selected");
                primarySlot.RemoveFromClassList("selected");
            }
        }

        private void Update()
        {
            if (_arcHud == null || _playerController == null) return;

            // Efficiently poll and update integer cooldowns every frame
            foreach (var kvp in _trackedLoadout)
            {
                AbilitySlot slotType = kvp.Key;
                AbilityData ability = kvp.Value;

                if (ability == null) continue;

                int uiIndex = MapSlotToIndex(slotType);
                SpellSlot uiSlot = _arcHud.GetSlot(uiIndex);

                if (uiSlot != null)
                {
                    int remainingTurns = _playerController.Abilities.GetRemainingCooldown(ability);
                    uiSlot.SetCooldown(remainingTurns);
                }
            }
        }

        // --- EVENT CALLBACKS ---

        private void HandleAmmoChanged(int current, int max)
        {
            _arcHud?.GetAmmoDisplay()?.UpdateAmmo(current, max);
        }

        private void HandleLoadoutChanged(AbilitySlot slotType, AbilityData abilityData)
        {
            _trackedLoadout[slotType] = abilityData;

            int uiIndex = MapSlotToIndex(slotType);
            SpellSlot uiSlot = _arcHud?.GetSlot(uiIndex);

            if (uiSlot != null)
            {
                // Dynamically fetch the icon from the scriptable object.
                // If abilityData is null (e.g., slot is unequipped), it passes null to clear the UI.
                Sprite iconToSet = abilityData != null ? abilityData.Icon : null; 
                uiSlot.SetAbility(iconToSet);
            }
        }

        // --- TRANSLATOR ---

        /// <summary>
        /// Translates the gameplay Enum into the linear UI array indexing.
        /// </summary>
        private int MapSlotToIndex(AbilitySlot slot)
        {
            return slot switch
            {
                AbilitySlot.Primary => 0,
                AbilitySlot.Secondary => 1,
                AbilitySlot.Dash => 2,
                AbilitySlot.Special => 3,
                _ => -1
            };
        }
    }
}