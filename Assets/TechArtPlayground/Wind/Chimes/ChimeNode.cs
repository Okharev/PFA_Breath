using UnityEngine;

namespace TechArtPlayground.Wind.Chimes
{
    public class ChimeNode : MonoBehaviour
    {
        [Tooltip("Heavier chimes resist the wind more.")]
        public float mass = 1.5f;
        
        [Tooltip("Longer chimes have a slower swing period.")]
        public float length = 2.0f;

        // Visual aid for artists in the editor
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            // Draw a small sphere at the pivot point
            Gizmos.DrawSphere(transform.position, 0.05f);
            
            // Draw a line representing the resting length of the chime
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawLine(transform.position, transform.position + (Vector3.down * length));
        }
    }
}