using System.Collections.Generic;
using Ability;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class ActionBarController : MonoBehaviour
    {
        [Header("References")]
        public PlayerAbilityController playerController;
        public VisualTreeAsset slotTemplate; // Drag AbilitySlot.uxml here

        private VisualElement actionBarContainer;
    
        // Maps the Ability ID to its specific UI slot controller
        private Dictionary<string, AbilitySlotUI> slotCache = new Dictionary<string, AbilitySlotUI>();

        private void Awake()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            actionBarContainer = root.Q<VisualElement>("action-bar-container");
        }

        private void Start()
        {
            // For a tactical shooter, you'd pass a list of the player's equipped loadout.
            // Let's pretend PlayerAbilityController exposes a method or property: GetLoadout()
            List<IAbility> playerLoadout = playerController.GetLoadout(); 
        
            InitializeBar(playerLoadout);
        }

        private void OnEnable() => TurnManager.OnTurnTicked += HandleTurnTicked;
        private void OnDisable() => TurnManager.OnTurnTicked -= HandleTurnTicked;

        private void InitializeBar(List<IAbility> abilities)
        {
            actionBarContainer.Clear();
            slotCache.Clear();

            for (int i = 0; i < abilities.Count; i++)
            {
                IAbility ability = abilities[i];

                // 1. Instantiate the UI Prefab
                VisualElement newSlot = slotTemplate.Instantiate();
            
                // 2. Wrap it in our helper class and initialize it
                AbilitySlotUI slotUI = new AbilitySlotUI(newSlot, ability);
            
                // Optional: Map a fake hotkey string just for visuals (1, 2, 3, etc.)
                slotUI.SetHotkey((i + 1).ToString()); 

                // 3. Add to the UI and Cache
                actionBarContainer.Add(newSlot);
                slotCache.Add(ability.AbilityId, slotUI);
            }
        }

        private void HandleTurnTicked(int currentTurn)
        {
            // Update all slots when a turn ticks
            foreach (var kvp in slotCache)
            {
                kvp.Value.RefreshUI();
            }
        }
    }

    /// <summary>
    /// A non-MonoBehaviour helper class to manage the state of a single instantiated slot.
    /// </summary>
    public class AbilitySlotUI
    {
        private readonly IAbility boundAbility;
        private readonly VisualElement rootElement;
        private readonly Label nameLabel;
        private readonly Label cooldownLabel;
        private readonly Label hotkeyLabel;
        private readonly ProgressBar channelBar;

        public AbilitySlotUI(VisualElement root, IAbility ability)
        {
            rootElement = root.Q<VisualElement>("slot-root");
            boundAbility = ability;
        
            nameLabel = root.Q<Label>("ability-name");
            cooldownLabel = root.Q<Label>("cooldown-overlay");
            hotkeyLabel = root.Q<Label>("hotkey-label");
            channelBar = root.Q<ProgressBar>("channel-bar");

            nameLabel.text = ability.AbilityId.Replace("_", "\n"); // Split over long names
            RefreshUI();
        }

        public void SetHotkey(string key) => hotkeyLabel.text = key;

        public void RefreshUI()
        {
            if (!boundAbility.IsReady && !boundAbility.IsChanneling)
            {
                cooldownLabel.style.display = DisplayStyle.Flex;
                cooldownLabel.text = boundAbility.CurrentCooldown.ToString();
                rootElement.AddToClassList("ability-on-cooldown");
            }
            else
            {
                cooldownLabel.style.display = DisplayStyle.None;
                rootElement.RemoveFromClassList("ability-on-cooldown");
            }

            if (boundAbility.IsChanneling)
            {
                channelBar.style.display = DisplayStyle.Flex;
                channelBar.highValue = boundAbility.RequiredChannelTime;
                channelBar.value = boundAbility.CurrentChannelTime;
            }
            else
            {
                channelBar.style.display = DisplayStyle.None;
            }
        }
    }
}