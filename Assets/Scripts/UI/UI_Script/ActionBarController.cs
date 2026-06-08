using System.Collections.Generic;
using Ability.NewAbilitySystem; 
using Skills; 
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class ActionBarController : MonoBehaviour
    {
        [Header("References")]
        public Ability.NewAbilitySystem.PlayerController player; 
        public VisualTreeAsset slotTemplate; 

        private VisualElement actionBarContainer;
        private readonly Dictionary<AbilitySlot, AbilitySlotUI> slotCache = new();

        private void Awake()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            actionBarContainer = root.Q<VisualElement>("action-bar-container");
            actionBarContainer.Clear();
        }

        private void OnEnable()
        {
            TurnManager.OnTurnTicked += HandleTurnTicked;
            
            if (player != null)
            {
                player.OnLoadoutChanged += HandleLoadoutChanged;
                
                // ARCHITECTURAL FIX: Lazy-evaluate the component. 
                // If this OnEnable fires before PlayerController.Awake(), we safely fetch the reference directly.
                AbilityController targetAbilities = player.Abilities != null ? player.Abilities : player.GetComponent<AbilityController>();
                
                if (targetAbilities != null)
                {
                    targetAbilities.OnAbilityExecuted += HandleAbilityExecuted; 
                }
            }
        }

        private void OnDisable()
        {
            TurnManager.OnTurnTicked -= HandleTurnTicked;
            
            if (player != null)
            {
                player.OnLoadoutChanged -= HandleLoadoutChanged;
                
                // Ensure we cleanly unsubscribe using the same fallback logic to prevent memory leaks
                AbilityController targetAbilities = player.Abilities != null ? player.Abilities : player.GetComponent<AbilityController>();
                
                if (targetAbilities != null)
                {
                    targetAbilities.OnAbilityExecuted -= HandleAbilityExecuted;
                }
            }
        }

        private void HandleLoadoutChanged(AbilitySlot slot, AbilityData ability)
        {
            // If the player unequipped something AND had no default, destroy the slot
            if (ability == null)
            {
                if (slotCache.TryGetValue(slot, out AbilitySlotUI slotUI))
                {
                    actionBarContainer.Remove(slotUI.RootElement);
                    slotCache.Remove(slot);
                }
            }
            else
            {
                // If it already exists, update the data to the new ability
                if (slotCache.TryGetValue(slot, out AbilitySlotUI existingSlot))
                {
                    existingSlot.BindAbility(ability);
                }
                else // Create a new UI slot for this ability
                {
                    VisualElement newSlot = slotTemplate.Instantiate();
                    
                    // Pass the player's internal ability controller so the UI can read cooldowns
                    AbilitySlotUI slotUI = new AbilitySlotUI(newSlot, player.Abilities); 
                    
                    slotUI.BindAbility(ability);
                    slotUI.SetHotkey(slot.ToString()); 

                    // Standard ordering setup (Primary -> Secondary -> Dash -> Special) 
                    // ensures they appear left-to-right correctly inside Flexbox
                    // 1. Store the enum value inside the visual element's memory
                    newSlot.userData = slot; 

                    // 2. Add it to the container
                    actionBarContainer.Add(newSlot);
                    slotCache.Add(slot, slotUI);

                    // 3. Sort the container's hierarchy based on the enum integer values
                    actionBarContainer.Sort((a, b) => ((AbilitySlot)a.userData).CompareTo((AbilitySlot)b.userData));
                }
            }
        }
        
        private void HandleAbilityExecuted()
        {
            int currentTurn = TurnManager.Instance != null ? TurnManager.Instance.CurrentTurn : 0;
            // No more +1 fakeout needed!
            HandleTurnTicked(currentTurn);
        }

        private void HandleTurnTicked(int currentTurn)
        {
            foreach (KeyValuePair<AbilitySlot, AbilitySlotUI> kvp in slotCache)
            {
                kvp.Value.RefreshUI(currentTurn);
            }
        }
    }

    /// <summary>
    /// Helper class - Remains exactly the same as our previous refactor!
    /// </summary>
    public class AbilitySlotUI
    {
        public VisualElement RootElement { get; private set; }
        
        private AbilityData boundAbility;
        private AbilityController controller;
        
        private readonly Label nameLabel;
        private readonly Label cooldownLabel;
        private readonly Label hotkeyLabel;
        private readonly ProgressBar channelBar;

        public AbilitySlotUI(VisualElement root, AbilityController abilityController)
        {
            RootElement = root.Q<VisualElement>("slot-root");
            controller = abilityController;
        
            nameLabel = root.Q<Label>("ability-name");
            cooldownLabel = root.Q<Label>("cooldown-overlay");
            hotkeyLabel = root.Q<Label>("hotkey-label");
            channelBar = root.Q<ProgressBar>("channel-bar");
        }

        public void BindAbility(AbilityData newAbility)
        {
            boundAbility = newAbility;
            nameLabel.text = boundAbility.abilityName.Replace(" ", "\n"); 
            
            int currentTurn = TurnManager.Instance != null ? TurnManager.Instance.CurrentTurn : 0;
            RefreshUI(currentTurn);
        }

        public void SetHotkey(string key) => hotkeyLabel.text = key;

        public void RefreshUI(int currentTurn)
        {
            if (boundAbility == null || controller == null) return;

            // Just ask the controller for the raw integer
            int remainingCooldown = controller.GetRemainingCooldown(boundAbility);

            if (remainingCooldown > 0)
            {
                cooldownLabel.style.display = DisplayStyle.Flex;
                cooldownLabel.text = remainingCooldown.ToString();
                RootElement.AddToClassList("ability-on-cooldown");
            }
            else
            {
                cooldownLabel.style.display = DisplayStyle.None;
                RootElement.RemoveFromClassList("ability-on-cooldown");
            }

            channelBar.style.display = DisplayStyle.None; 
        }
    }
}