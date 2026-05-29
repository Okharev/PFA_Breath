using UnityEngine;

namespace TechArtPlayground.Cloth
{
    public class PhysicsBannerNode : MonoBehaviour
    {
        [Header("Cloth Shape")] public Vector2Int resolution = new(16, 16); // Plus petit par défaut pour les perfs

        public Vector2 dimensions = new(2f, 4f);

        [Header("Prayer Flag Mode")] public bool isPrayerFlagMode;

        public int flagWidth = 5;
        [Range(0.0f, 1.0f)] public float ropeTension = 0.5f;

        // Repère visuel
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.down * (dimensions.y / 2f),
                new Vector3(dimensions.x, dimensions.y, 0.1f));
        }
    }
}