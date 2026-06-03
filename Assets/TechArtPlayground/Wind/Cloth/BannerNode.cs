using UnityEngine;

namespace TechArtPlayground.Cloth
{
    public class PhysicsBannerNode : MonoBehaviour
    {
        [Header("Cloth Shape")] public Vector2Int resolution = new(16, 16); // Plus petit par défaut pour les perfs

        [Header("Physics Painting")]
        [Tooltip("Red Channel = Inverse Mass (0 is pinned). Green Channel = Stiffness multiplier.")]
        public Texture2D weightMap;
        
        public Vector2 dimensions = new(2f, 4f);

        [Header("Prayer Flag Mode")] public bool isPrayerFlagMode;

        public int flagWidth = 5;
        [Range(0.0f, 1.0f)] public float ropeTension = 0.5f;

        // Repère visuel
        private void OnDrawGizmos()
        {
            // 1. Cache the original matrix to avoid affecting other gizmos
            Matrix4x4 originalMatrix = Gizmos.matrix;

            // 2. Set the Gizmo matrix to this object's local-to-world transform
            // This automatically handles Position, Rotation, and Scale
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = Color.cyan;

            // 3. Define the center in local space.
            // Since the pivot is typically at the top center, we offset downward 
            // by half the height.
            Vector3 localCenter = new Vector3(0, -dimensions.y / 2f, 0);

            // 4. Draw the cube relative to the new matrix
            Gizmos.DrawWireCube(localCenter, new Vector3(dimensions.x, dimensions.y, 0.1f));

            // 5. Restore the original matrix
            Gizmos.matrix = originalMatrix;
        }
    }
}