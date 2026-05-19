using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class OxygenWorldUI : MonoBehaviour
    {
        [Header("Tracking")]
        public Transform playerTarget;
        public Vector3 worldOffset = new Vector3(1f, 1.5f, 0f);

        private UIDocument uiDocument;
        private VisualElement containerElement;
        private RadialOxygenBar radialFillElement;
        private PlayerOxygen playerOxygen;
        private Camera mainCamera;

        private void OnEnable()
        {
            uiDocument = GetComponent<UIDocument>();
            mainCamera = Camera.main;

            var root = uiDocument.rootVisualElement;
            containerElement = root.Q<VisualElement>("OxygenContainer");
            radialFillElement = root.Q<RadialOxygenBar>("RadialFill");

            // Hide initially
            if (containerElement != null) containerElement.style.opacity = 0f;

            if (playerTarget is not null)
            {
                playerOxygen = playerTarget.GetComponent<PlayerOxygen>();
                if (playerOxygen is not null)
                {
                    playerOxygen.OnOxygenChanged += HandleOxygenChanged;
                }
            }
        }

        private void OnDisable()
        {
            if (playerOxygen is not null) playerOxygen.OnOxygenChanged -= HandleOxygenChanged;
        }

        private void LateUpdate()
        {
            // Continuously project the 3D position to the 2D screen
            if (playerTarget is not null && containerElement != null && containerElement.resolvedStyle.opacity > 0)
            {
                Vector3 targetPos = playerTarget.position + worldOffset;
            
                // Unity's built in world-to-UI-Toolkit converter
                Vector2 screenPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                    containerElement.panel, 
                    targetPos, 
                    mainCamera
                );

                // Move the container. The USS "translate: -50% -50%" handles the centering.
                containerElement.style.left = screenPos.x;
                containerElement.style.top = screenPos.y;
            }
        }

        private void HandleOxygenChanged(int current, int max)
        {
            if (containerElement == null || radialFillElement == null) return;

            // 1. Update the Vector Graphic
            radialFillElement.Progress = (float)current / max;

            // 2. Breath of the Wild Fade Logic
            if (current < max)
            {
                containerElement.style.opacity = 1f; // Fades in smoothly via USS
            }
            else
            {
                containerElement.style.opacity = 0f; // Fades out smoothly via USS
            }
        }
    }
}