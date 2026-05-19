using UnityEngine;

/// <summary>
///     Gère le déplacement de la balle.
///     Utilise la physique cinématique pour respecter l'arrêt du temps.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Paramètres de la balle")] public float speed = 15f;

    [Tooltip("Durée de vie de la balle en secondes de jeu (pas en temps réel)")]
    public float lifeTime = 5f;

    private float currentLifeTime;

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // On détruit la balle après un certain temps de jeu pour nettoyer la scène.
        // On utilise Time.deltaTime (qui est à 0 quand le jeu est en pause).
        currentLifeTime += Time.deltaTime;
        if (currentLifeTime >= lifeTime) Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        // Comme pour le joueur, FixedUpdate ne tourne pas si Time.timeScale = 0.
        // La balle va donc se figer dans les airs entre les actions !

        // Calcul de la nouvelle position vers l'avant
        Vector3 newPosition = rb.position + transform.forward * (speed * Time.fixedDeltaTime);
        rb.MovePosition(newPosition);
    }
}