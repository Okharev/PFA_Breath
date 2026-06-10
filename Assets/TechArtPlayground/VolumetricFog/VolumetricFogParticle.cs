using System.Collections.Generic;
using UnityEngine;

namespace TechArtPlayground.VolumetricFog
{
    [ExecuteInEditMode]
    public class VolumetricFogParticle : MonoBehaviour
    {
        public float radius = 5f;
        [Range(0f, 5f)] public float densityMultiplier = 1f;

        // Global tracking list for the renderer feature to grab
        public static List<VolumetricFogParticle> ActiveParticles = new List<VolumetricFogParticle>();

        private void OnEnable() => ActiveParticles.Add(this);
        private void OnDisable() => ActiveParticles.Remove(this);

        private void OnDrawGizmosSelected()
        {
            // Draw the boundary of the particle fog puff in the scene view
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
            Gizmos.DrawSphere(transform.position, radius);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}