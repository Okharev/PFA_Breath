using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
///     A universal health component for the Player, Enemies, or destructible objects.
/// </summary>
public class HealthComponent : MonoBehaviour
{
    [Header("Health Settings")] public float maxHealth = 100f;

    [Header("Events")] public UnityEvent<float, float> OnHealthChanged; // Passes (Current, Max)

    public  event Action<float> OnTakeDamage; // Passes (Damage Amount)
    public  event Action<float> OnHealed; // Passes (Heal Amount)
    public event Action<HealthComponent> OnDeath;
    
    private float currentHealth;

    private bool isDead;

    private void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0) return;

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
        OnDeath?.Invoke(this);

        // You can handle destroying the object via the UnityEvent in the Inspector,
        // or just Destroy it here directly.
        Destroy(gameObject);
    }
}