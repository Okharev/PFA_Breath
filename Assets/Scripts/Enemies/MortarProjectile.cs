using UnityEngine;

namespace Enemies
{
    public class MortarProjectile : MonoBehaviour
    {
        [Header("Trajectory Settings")] public float arcHeight = 5f;

        public float splashRadius = 1.5f;

        [Header("Effects")] public GameObject impactEffectPrefab;

        private float elapsedTime;
        private Vector3 endPos;
        private bool isLaunched;

        private Vector3 startPos;

        private float totalFlightTime;

        private void Update()
        {
            if (!isLaunched) return;

            // Because TurnManager sets timeScale to 0 between turns, 
            // this naturally pauses the arc when the turn is over!
            elapsedTime += Time.deltaTime;

            // Normalize time between 0 and 1
            float t = Mathf.Clamp01(elapsedTime / totalFlightTime);

            Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);

            // Add the parabolic arc on the Y axis
            currentPos.y += arcHeight * Mathf.Sin(t * Mathf.PI);

            transform.position = currentPos;

            if (t >= 1f) Explode();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, splashRadius);
        }

        /// <summary>
        ///     Launches the projectile to land after a specific number of turns.
        /// </summary>
        public void Launch(Vector3 targetPosition, int turnsToLand)
        {
            startPos = transform.position;
            endPos = targetPosition;

            // Calculate real-time seconds this projectile will spend in the air
            totalFlightTime = turnsToLand * TurnManager.Instance.secondsPerTurn;
            isLaunched = true;
        }

        private void Explode()
        {
            if (impactEffectPrefab != null) Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);

            // Detect player in splash zone (O(K) where K is colliders in range)
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, splashRadius);
            foreach (Collider hit in hitColliders)
                if (hit.CompareTag("Player"))
                    Debug.Log("Player struck by mortar splash!");
            // TODO: Apply damage script here
            Destroy(gameObject);
        }
    }
}