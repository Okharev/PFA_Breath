namespace TechArtPlayground.Oasis
{
    using UnityEngine;

    [ExecuteAlways]
    public class OasisController : MonoBehaviour
    {
        [Header("Oasis Parameters")]
        public Transform oasisCenter;
        public float maxRadius = 50f;
        public float expansionSpeed = 5f;
    
        [Header("State")]
        public bool isExpanding = false;
    
        private float currentRadius = 0f;

        // Property IDs for slight performance gain over string lookups
        private static readonly int GlobalOasisCenterID = Shader.PropertyToID("_GlobalOasisCenter");
        private static readonly int GlobalOasisRadiusID = Shader.PropertyToID("_GlobalOasisRadius");

        void Update()
        {
            if (oasisCenter == null) return;

            // Handle Expansion Logic
            if (isExpanding && currentRadius < maxRadius)
            {
                currentRadius += expansionSpeed * Time.deltaTime;
            }
            else if (!isExpanding && currentRadius > 0f)
            {
                currentRadius -= expansionSpeed * Time.deltaTime;
            }
        
            currentRadius = Mathf.Clamp(currentRadius, 0f, maxRadius);

            // Update Global Shader Variables
            Shader.SetGlobalVector(GlobalOasisCenterID, oasisCenter.position);
            Shader.SetGlobalFloat(GlobalOasisRadiusID, currentRadius);
        }

        // Trigger this from gameplay events
        public void TriggerOasis()
        {
            isExpanding = true;
        }
    }
}