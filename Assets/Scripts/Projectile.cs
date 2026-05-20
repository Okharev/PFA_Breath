using UnityEngine;

/// <summary>
///     Gère le déplacement de la balle via Raycast.
///     Ne nécessite PLUS de Rigidbody. Respecte le Time.timeScale naturellement.
/// </summary>
public class Projectile : MonoBehaviour
{
    [Header("Paramètres de la balle")] public float speed = 15f;

    public float damage = 25f;

    [Tooltip("Les calques (Layers) que la balle peut toucher (Ex: Player, Environment)")]
    public LayerMask hitMask;

    [Tooltip("Durée de vie de la balle en secondes de jeu")]
    public float lifeTime = 5f;

    private float currentLifeTime;
    private Vector3 lastPosition;


    private void Start()
    {
        lastPosition = transform.position;
    }

    private void Update()
    {
        // 1. Handle Lifetime (Time.deltaTime is 0 when time is paused)
        currentLifeTime += Time.deltaTime;
        if (currentLifeTime >= lifeTime)
        {
            Destroy(gameObject); // (Consider changing to an Object Pool later)
            return;
        }

        // 2. Calculate movement
        float moveDistance = speed * Time.deltaTime;
        Vector3 direction = transform.forward;

        // 3. Raycast for continuous collision detection
        // We cast a ray from where the bullet WAS to where it WILL BE this frame
        if (Physics.Raycast(lastPosition, direction, out RaycastHit hit, moveDistance, hitMask))
        {
            // We hit something! Apply damage if possible
            if (hit.collider.TryGetComponent(out HealthComponent damageable)) damageable.TakeDamage(damage);

            // TODO: Spawn hit VFX at hit.point here

            // Destroy the projectile immediately
            Destroy(gameObject);
        }
        else
        {
            // 4. Move if no collision
            transform.position += direction * moveDistance;
            lastPosition = transform.position;
        }
    }
}