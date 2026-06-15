using Ability.NewAbilitySystem;
using Dialogues.UI;
using TechArtPlayground.Oasis;
using UnityEngine;
using UnityEngine.UIElements;

// Sourced from OxygenComponent namespace

namespace UI
{
    /// <summary>
    ///     Controller for the Player's top-left HUD stats.
    ///     Utilizes the Observer pattern to react dynamically to stat changes.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PlayerStatsUIController : MonoBehaviour
    {
        [Header("Data Models")] [Tooltip("Reference to the player's health component.")] [SerializeField]
        private HealthComponent playerHealth;

        [Tooltip("Reference to the player's oxygen component.")] [SerializeField]
        private OxygenComponent playerOxygen;

        private DoorObjectives doorObjectives;


        // View Elements
        private VisualElement healthBarFill;
        private VisualElement oxygenBarFill;

        private Label roomCleartext;


        private void OnEnable()
        {
            // 1. Query the Visual Elements using UI Toolkit
            UIDocument uiDoc = GetComponent<UIDocument>();
            VisualElement root = uiDoc.rootVisualElement;

            healthBarFill = root.Q<VisualElement>("health-bar-fill");
            oxygenBarFill = root.Q<VisualElement>("oxygen-bar-fill");
            roomCleartext = root.Q<Label>("Nmb_Door");

            // 2. Subscribe to events (Observer Pattern)
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged.AddListener(UpdateHealthUI);
                UpdateHealthUI(playerHealth.CurrentHealth, playerHealth.maxHealth); // Init
            }

            if (playerOxygen != null)
            {
                playerOxygen.OnOxygenChanged += UpdateOxygenUI;
                UpdateOxygenUI(playerOxygen.CurrentOxygen, playerOxygen.maxOxygen); // Init
            }

            if (roomCleartext != null)
            {
                doorObjectives = GetComponent<DoorObjectives>();
                doorObjectives.onDoorNumberChanged += UpdateDoorNb;
                UpdateDoorNb(doorObjectives.currentNbRoom, doorObjectives.maxNbRoom); // Init
            }
        }

        private void OnDisable()
        {
            // Always decouple events to prevent memory leaks when the UI is disabled
            if (playerHealth != null) playerHealth.OnHealthChanged.RemoveListener(UpdateHealthUI);

            if (playerOxygen != null) playerOxygen.OnOxygenChanged -= UpdateOxygenUI;
        }

        /// <summary>
        ///     Converts current health into a UI percentage width.
        ///     Time Complexity: O(1)
        /// </summary>
        private void UpdateHealthUI(float currentHealth, float maxHealth)
        {
            if (healthBarFill == null || maxHealth <= 0) return;

            float percentage = currentHealth / maxHealth * 100f;
            healthBarFill.style.width = Length.Percent(percentage);
        }

        /// <summary>
        ///     Converts current oxygen into a UI percentage width.
        ///     Time Complexity: O(1)
        /// </summary>
        private void UpdateOxygenUI(float currentOxygen, float maxOxygen)
        {
            if (oxygenBarFill == null || maxOxygen <= 0) return;

            float percentage = currentOxygen / maxOxygen * 100f;
            oxygenBarFill.style.width = Length.Percent(percentage);
        }


        /// <summary>
        ///     Add a room unlocked to the final door
        /// </summary>
        /// 
        private void UpdateDoorNb(int currentDoor, int maxDoor)
        {
            if (roomCleartext == null || doorObjectives == null) return;

            roomCleartext.text = $"{currentDoor}/{maxDoor}";
        }
    }
}