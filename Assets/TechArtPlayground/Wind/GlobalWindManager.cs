using UnityEngine;

namespace TechArtPlayground.Wind
{
    [DefaultExecutionOrder(-100)] // S'exécute en premier pour mettre à jour le vent avant la physique
    [ExecuteAlways] // Permet de voir le vent global même dans l'éditeur sans lancer le mode Play !
    public class GlobalWindManager : MonoBehaviour
    {
        [Header("Global Wind Settings")] [Tooltip("Direction and speed of the wind.")]
        public Vector3 windVelocity = new(5f, 0f, 2f);

        [Tooltip("How chaotic the wind is across the world.")] [Range(0f, 5f)]
        public float windTurbulence = 1.5f;

        public static GlobalWindManager Instance { get; private set; }

        private void Update()
        {
            // Met à jour les variables globales pour tous les Shaders classiques (comme les Bannières)
            Shader.SetGlobalVector("_GlobalWindVelocity", windVelocity);
            Shader.SetGlobalFloat("_GlobalWindTurbulence", windTurbulence);
        }

        private void OnEnable()
        {
            Instance = this;
        }

        // --- NOUVEAU : Dessin de la flèche de debug ---
        private void OnDrawGizmos()
        {
            if (windVelocity.sqrMagnitude < 0.01f) return;

            // Couleur cyan transparente pour représenter le vent
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);

            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + windVelocity;

            // Dessine la ligne principale dont la longueur est proportionnelle à la vitesse
            Gizmos.DrawLine(startPos, endPos);

            // Dessine la base (origine)
            Gizmos.DrawSphere(startPos, 0.2f);

            // Calcule et dessine la pointe de la flèche (Arrowhead)
            Vector3 direction = windVelocity.normalized;

            // La taille de la pointe s'adapte légèrement à la puissance du vent, avec des limites
            float arrowHeadLength = Mathf.Clamp(windVelocity.magnitude * 0.15f, 0.3f, 2f);

            // On calcule les 4 ailettes de la flèche en utilisant des rotations
            Quaternion lookRot = Quaternion.LookRotation(direction);
            Vector3 rightWing = lookRot * Quaternion.Euler(0, 150, 0) * Vector3.forward;
            Vector3 leftWing = lookRot * Quaternion.Euler(0, -150, 0) * Vector3.forward;
            Vector3 upWing = lookRot * Quaternion.Euler(150, 0, 0) * Vector3.forward;
            Vector3 downWing = lookRot * Quaternion.Euler(-150, 0, 0) * Vector3.forward;

            // Dessine la tête de flèche en 3D
            Gizmos.DrawLine(endPos, endPos + rightWing * arrowHeadLength);
            Gizmos.DrawLine(endPos, endPos + leftWing * arrowHeadLength);
            Gizmos.DrawLine(endPos, endPos + upWing * arrowHeadLength);
            Gizmos.DrawLine(endPos, endPos + downWing * arrowHeadLength);
        }
    }
}