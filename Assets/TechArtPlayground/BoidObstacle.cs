using UnityEngine;

namespace TechArtPlayground
{
    public class BoidObstacle : MonoBehaviour
    {
        public BoidSwarm targetSwarm;
        
        [Tooltip("If true, automatically reads size from Box/Sphere colliders attached to this object.")]
        public bool autoFetchFromCollider = true;

        public enum ShapeType { Sphere = 0, Box = 1 }
        public ShapeType shapeType = ShapeType.Sphere;
        public Vector3 extents = Vector3.one;

        private void OnEnable()
        {
            if (autoFetchFromCollider) FetchColliderData();
            if (targetSwarm != null) targetSwarm.RegisterObstacle(this);
        }

        private void OnDisable()
        {
            if (targetSwarm != null) targetSwarm.UnregisterObstacle(this);
        }

        private void FetchColliderData()
        {
            if (TryGetComponent<SphereCollider>(out var sphere))
            {
                shapeType = ShapeType.Sphere;
                extents = new Vector3(sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z), 0, 0);
            }
            else if (TryGetComponent<BoxCollider>(out var box))
            {
                shapeType = ShapeType.Box;
                // Multiply half-extents by lossy scale to get true world size
                extents = Vector3.Scale(box.size * 0.5f, transform.lossyScale);
            }
        }
        
        // Visualize the SDF boundaries in the editor
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            
            if (autoFetchFromCollider) FetchColliderData();

            if (shapeType == ShapeType.Box)
                Gizmos.DrawWireCube(Vector3.zero, extents * 2f);
            else
                Gizmos.DrawWireSphere(Vector3.zero, extents.x);
        }
    }
}