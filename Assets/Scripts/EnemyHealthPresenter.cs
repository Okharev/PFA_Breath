using UI;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(HealthComponent))]
public class EnemyHealthPresenter : MonoBehaviour
{
    [Header("UI Settings")]
    [Tooltip("How high above the object origin should the bar float?")]
    [SerializeField] private float yOffset = 2.0f;
    
    private HealthComponent _healthComponent;
    private VisualElement _healthContainer;
    private VisualElement _healthFill;
    private Camera _mainCamera;

    private void Start()
    {
        _healthComponent = GetComponent<HealthComponent>();
        _mainCamera = Camera.main;

        // Request a UI element from the Object Pool (O(1) operation)
        _healthContainer = UIHealthBarManager.Instance.GetHealthBar();
        _healthFill = _healthContainer.Q<VisualElement>("health-fill");

        // Subscribe to our domain events (Observer Pattern)
        _healthComponent.OnHealthChanged.AddListener(UpdateHealthUI);
        _healthComponent.OnTakeDamage += FlashUI;
        
        // Initialize state
        UpdateHealthUI(_healthComponent.CurrentHealth, _healthComponent.maxHealth);
    }

    private void LateUpdate()
    {
        // Calculate the world position with offset
        Vector3 targetWorldPos = transform.position + (Vector3.up * yOffset);

        // Calculate view space to ensure we don't draw UI for targets behind the camera
        Vector3 viewportPos = _mainCamera.WorldToViewportPoint(targetWorldPos);

        if (viewportPos.z < 0)
        {
            _healthContainer.style.display = DisplayStyle.None;
            return;
        }

        _healthContainer.style.display = DisplayStyle.Flex;

        // --- THE CRITICAL WORLD-TO-SCREEN TRANSLATION ---
        // RuntimePanelUtils correctly maps coordinates taking UI scaling into account.
        Vector2 panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(
            _healthContainer.panel, targetWorldPos, _mainCamera);

        _healthContainer.style.left = panelPosition.x;
        _healthContainer.style.top = panelPosition.y;
    }

    private void UpdateHealthUI(float currentHealth, float maxHealth)
    {
        // Calculate percentage (O(1) Time)
        float percentage = Mathf.Clamp01(currentHealth / maxHealth) * 100f;
        
        // CSS transitions automatically handle the smooth interpolation of this width change
        _healthFill.style.width = new Length(percentage, LengthUnit.Percent);
    }

    private void FlashUI(float damageAmount)
    {
        // Add the white flash class
        _healthFill.AddToClassList("hit-flash");

        // Use UI Toolkit's built-in scheduler to remove the flash after 100ms.
        // This is much more memory efficient than starting a Coroutine.
        _healthFill.schedule.Execute(() => 
        {
            if (_healthFill != null) 
                _healthFill.RemoveFromClassList("hit-flash");
        }).StartingIn(100);
    }

    private void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (_healthComponent != null)
        {
            _healthComponent.OnHealthChanged.RemoveListener(UpdateHealthUI);
            _healthComponent.OnTakeDamage -= FlashUI;
        }

        // Return the UI element to the pool safely
        if (_healthContainer != null && UIHealthBarManager.Instance != null)
        {
            UIHealthBarManager.Instance.ReleaseHealthBar(_healthContainer);
        }
    }
}