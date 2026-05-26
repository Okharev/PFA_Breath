using UnityEngine;

namespace TechArtPlayground.Water
{
    [ExecuteAlways]
    public class OceanGridFollower : MonoBehaviour
    {
        [Header("Target References")]
        public Transform targetCamera;
        public OceanFFTBinder oceanSimulation;

        [Header("Settings")]
        public float oceanYHeight = 0f;

        private Camera camera1;

        private void Start()
        {
            camera1 = Camera.main;
        }

        void LateUpdate()
        {
            if (targetCamera == null)
            {
                if (camera1 != null) targetCamera = Camera.main.transform;
                else return;
            }

            if (oceanSimulation == null) return;

            // Calculate the physical distance between each pixel in the FFT texture
            float gridSpacing = oceanSimulation.oceanSize / oceanSimulation.resolution;

            // Get the camera's raw world position
            Vector3 camPos = targetCamera.position;

            // Quantize (Snap) the position to the exact grid spacing
            // This completely eliminates vertex swimming and shimmering
            float snappedX = Mathf.Round(camPos.x / gridSpacing) * gridSpacing;
            float snappedZ = Mathf.Round(camPos.z / gridSpacing) * gridSpacing;

            // Update the LOD group position
            transform.position = new Vector3(snappedX, oceanYHeight, snappedZ);
        }
    }
}