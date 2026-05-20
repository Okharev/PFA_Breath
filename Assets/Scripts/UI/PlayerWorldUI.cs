using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class PlayerWorldUI : MonoBehaviour
    {
        [Header("Tracking Targets")] public Transform playerTarget;

        [Header("World Offsets")] [Tooltip("Positive X places it to the right of the player")]
        public Vector3 oxygenWorldOffset = new(1.2f, 1.5f, 0f);

        [Tooltip("Negative X places it to the left of the player")]
        public Vector3 healthWorldOffset = new(-1.2f, 1.5f, 0f);

        private VisualElement healthContainer;
        private RadialProgressBar healthFill;
        private Camera mainCamera;

        // UI Elements
        private VisualElement oxygenContainer;
        private RadialProgressBar oxygenFill;
        private HealthComponent playerHealth;

        // Logic Components
        private PlayerOxygen playerOxygen;

        private UIDocument uiDocument;

        private void LateUpdate()
        {
            if (playerTarget is null) return;

            // 1. Position Oxygen Bar (Right Side)
            if (oxygenContainer != null && oxygenContainer.resolvedStyle.opacity > 0)
            {
                Vector2 oxyScreenPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                    oxygenContainer.panel, playerTarget.position + oxygenWorldOffset, mainCamera);
                oxygenContainer.style.left = oxyScreenPos.x;
                oxygenContainer.style.top = oxyScreenPos.y;
            }

            // 2. Position Health Bar (Left Side)
            if (healthContainer != null && healthContainer.resolvedStyle.opacity > 0)
            {
                Vector2 hpScreenPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                    healthContainer.panel, playerTarget.position + healthWorldOffset, mainCamera);
                healthContainer.style.left = hpScreenPos.x;
                healthContainer.style.top = hpScreenPos.y;
            }
        }

        private void OnEnable()
        {
            uiDocument = GetComponent<UIDocument>();
            mainCamera = Camera.main;

            VisualElement root = uiDocument.rootVisualElement;

            // Map Oxygen Elements
            oxygenContainer = root.Q<VisualElement>("OxygenContainer");
            oxygenFill = root.Q<RadialProgressBar>("OxygenRadialFill");

            // Map Health Elements
            healthContainer = root.Q<VisualElement>("HealthContainer");
            healthFill = root.Q<RadialProgressBar>("HealthRadialFill");

            // Hide initially
            if (oxygenContainer != null) oxygenContainer.style.opacity = 0f;
            if (healthContainer != null) healthContainer.style.opacity = 0f;

            if (playerTarget is not null)
            {
                playerOxygen = playerTarget.GetComponent<PlayerOxygen>();
                playerHealth = playerTarget.GetComponent<HealthComponent>();

                if (playerOxygen is not null) playerOxygen.OnOxygenChanged += HandleOxygenChanged;
                if (playerHealth is not null) playerHealth.OnHealthChanged.AddListener(HandleHealthChanged);
            }
        }

        private void OnDisable()
        {
            if (playerOxygen is not null) playerOxygen.OnOxygenChanged -= HandleOxygenChanged;
            if (playerHealth is not null) playerHealth.OnHealthChanged.RemoveListener(HandleHealthChanged);
        }

        private void HandleOxygenChanged(int current, int max)
        {
            if (oxygenContainer == null || oxygenFill == null) return;

            oxygenFill.Progress = (float)current / max;
            oxygenContainer.style.opacity = current < max ? 1f : 0f;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (healthContainer == null || healthFill == null) return;

            healthFill.Progress = current / max;

            // Fades in when damaged, fades out when at full health
            healthContainer.style.opacity = current < max ? 1f : 0f;
        }
    }
}