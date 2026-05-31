using UnityEngine;

namespace TechArtPlayground.Wind.Cloth
{
    public enum ClothColliderType { Sphere = 0, Capsule = 1, Box = 2 }

    public class ClothStaticCollider : MonoBehaviour
    {
        public ClothColliderType colliderType = ClothColliderType.Sphere;
        
        [Header("Sphere / Capsule")]
        public float radius = 0.5f;
        public float height = 2f; // Total height for capsule

        [Header("Box")]
        public Vector3 boxExtents = new Vector3(0.5f, 0.5f, 0.5f); // Half-size

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.5f);
            Matrix4x4 oldMat = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (colliderType == ClothColliderType.Sphere)
            {
                Gizmos.DrawWireSphere(Vector3.zero, radius);
            }
            else if (colliderType == ClothColliderType.Box)
            {
                Gizmos.DrawWireCube(Vector3.zero, boxExtents * 2);
            }
            else if (colliderType == ClothColliderType.Capsule)
            {
                float halfHeight = Mathf.Max(0, height * 0.5f - radius);
                Gizmos.DrawWireSphere(Vector3.up * halfHeight, radius);
                Gizmos.DrawWireSphere(Vector3.down * halfHeight, radius);
            }

            Gizmos.matrix = oldMat;
        }
    }
}