using System.Collections.Generic;
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

        private void OnEnable()
        {
            if (_uiDocument == null) return;

            // Query the parent UI element
            _arcHud = _uiDocument.rootVisualElement.Q<ArcHudPanel>();

            if (_arcHud == null)
            {
                Debug.LogWarning("ArcHudPanel not found in the provided UIDocument.");
                return;
            }

            // --- BIND EVENTS ---

            if (_ammoComponent != null)
            {
                _ammoComponent.OnAmmoChanged += HandleAmmoChanged;
                HandleAmmoChanged(_ammoComponent.CurrentAmmo, _ammoComponent.maxAmmo);
            }

            if (_playerController != null)
            {
                _playerController.OnLoadoutChanged += HandleLoadoutChanged;
            
                // Map hardcoded hotkeys visually to the slots
                _arcHud.GetSlot(MapSlotToIndex(AbilitySlot.Primary))?.SetHotkey("LMB");
                _arcHud.GetSlot(MapSlotToIndex(AbilitySlot.Secondary))?.SetHotkey("RMB");
                _arcHud.GetSlot(MapSlotToIndex(AbilitySlot.Dash))?.SetHotkey("SHF");
                _arcHud.GetSlot(MapSlotToIndex(AbilitySlot.Special))?.SetHotkey("R");
            }
        }

        private void OnDisable()
        {
            // Prevent memory leaks when the scene unloads or object is destroyed
            if (_ammoComponent != null)
                _ammoComponent.OnAmmoChanged -= HandleAmmoChanged;

            if (_playerController != null)
                _playerController.OnLoadoutChanged -= HandleLoadoutChanged;
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