using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
///     A universal health component for the Player, Enemies, or destructible objects.
/// </summary>
public class HealthComponent : MonoBehaviour
{
    [Header("Health Settings")] [Tooltip("The maximum health capacity of the entity.")]
    public float maxHealth = 100f;

    [Header("Events")] [Tooltip("Fired when health changes. Passes (CurrentHealth, MaxHealth).")]
    public UnityEvent<float, float> OnHealthChanged;

    // --- Invincibility Flag ---
    /// <summary>
    ///     When true, the entity ignores all incoming damage. Used for phasing, dashing, or i-frames.
    /// </summary>
    public bool IsInvincible { get; set; } = false;

    // Expose current health safely for UI or other systems to read without modifying
    public float CurrentHealth { get; private set; }

    public bool IsDead { get; private set; }

    private void Start()
    {
        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public event Action<float> OnTakeDamage; // Passes (Damage Amount)
    public event Action<float> OnHealed; // Passes (Heal Amount)
    public event Action<GameObject> OnDeath;

    public void TakeDamage(float amount)
    {
        // FAST FAIL: Check death, invalid amounts, OR if the entity is currently invincible.
        // This O(1) boolean check prevents unnecessary math and event invocations during a dash.
        if (IsDead || amount <= 0 || IsInvincible) return;

        CurrentHealth -= amount;

        // Clamp health so it doesn't drop below 0
        CurrentHealth = Mathf.Max(CurrentHealth, 0);

        OnTakeDamage?.Invoke(amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0) return;

        CurrentHealth += amount;
        CurrentHealth = Mathf.Min(CurrentHealth, maxHealth); // Prevent over-healing

        OnHealed?.Invoke(amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void Die()
    {
        IsDead = true;

        // Broadcast the death event BEFORE destroying the object, 
        // ensuring listeners (like TurnManager or UI) can safely read its data one last time.
        OnDeath?.Invoke(gameObject);

        // Architectural Note: Consider replacing Destroy with Object Pooling in the future!
        //Destroy(gameObject);
    }
}