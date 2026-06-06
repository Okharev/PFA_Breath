using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
///     A universal health component for the Player, Enemies, or destructible objects.
/// </summary>
public class HealthComponent : MonoBehaviour
{
    [Header("Health Settings")] 
    [Tooltip("The maximum health capacity of the entity.")]
    public float maxHealth = 100f;

    // --- Invincibility Flag ---
    /// <summary>
    ///     When true, the entity ignores all incoming damage. Used for phasing, dashing, or i-frames.
    /// </summary>
    public bool IsInvincible { get; set; } = false;

    [Header("Events")] 
    [Tooltip("Fired when health changes. Passes (CurrentHealth, MaxHealth).")]
    public UnityEvent<float, float> OnHealthChanged; 

    public event Action<float> OnTakeDamage; // Passes (Damage Amount)
    public event Action<float> OnHealed;     // Passes (Heal Amount)
    public event Action<GameObject> OnDeath;
    
    private float currentHealth;
    private bool isDead;

    // Expose current health safely for UI or other systems to read without modifying
    public float CurrentHealth => currentHealth; 
    public bool IsDead => isDead;

    private void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        // FAST FAIL: Check death, invalid amounts, OR if the entity is currently invincible.
        // This O(1) boolean check prevents unnecessary math and event invocations during a dash.
        if (isDead || amount <= 0 || IsInvincible) return;

        currentHealth -= amount;

        // Clamp health so it doesn't drop below 0
        currentHealth = Mathf.Max(currentHealth, 0);

        OnTakeDamage?.Invoke(amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0) Die();
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth); // Prevent over-healing

        OnHealed?.Invoke(amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        isDead = true;
        
        // Broadcast the death event BEFORE destroying the object, 
        // ensuring listeners (like TurnManager or UI) can safely read its data one last time.
        OnDeath?.Invoke(gameObject);

        // Architectural Note: Consider replacing Destroy with Object Pooling in the future!
        Destroy(gameObject);
    }
}