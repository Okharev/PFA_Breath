namespace TechArtPlayground.Oasis
{
    using UnityEngine;

    [ExecuteAlways]
    public class OasisNode : MonoBehaviour
    {
        [Header("Zone Settings")]
        [Tooltip("How large the grey, corrupted area is.")]
        public float deadZoneRadius = 50f;
        [Tooltip("How fast the healing color wave expands.")]
        public float expansionSpeed = 5f;
        
        [Header("State")]
        [Tooltip("If true, the zone starts fully healed (colored). If false, it starts fully grey.")]
        public bool startHealed = false;
        
        public bool isExpanding = false;
    
        public float CurrentWaveRadius { get; private set; } = 0f;

        void OnEnable()
        {
            if (OasisManager.Instance != null) OasisManager.Instance.RegisterOasis(this);

            if (startHealed)
            {
                CurrentWaveRadius = deadZoneRadius;
                isExpanding = true;
            }
            else
            {
                CurrentWaveRadius = 0f; // Starts fully grey
            }
        }

        void OnDisable()
        {
            if (OasisManager.Instance != null) OasisManager.Instance.DeregisterOasis(this);
        }

        void Update()
        {
            if (isExpanding && CurrentWaveRadius < deadZoneRadius)
            {
                CurrentWaveRadius += expansionSpeed * Time.deltaTime;
            }
            else if (!isExpanding && CurrentWaveRadius > 0f)
            {
                // Optional: Allows the corruption to creep back in if disabled
                CurrentWaveRadius -= expansionSpeed * Time.deltaTime; 
            }
        
            CurrentWaveRadius = Mathf.Clamp(CurrentWaveRadius, 0f, deadZoneRadius);
        }

        [ContextMenu("Trigger Oasis")]
        public void TriggerOasis() => isExpanding = true;
        
        [ContextMenu("Retract Oasis")]
        public void RetractOasis() => isExpanding = false;
        
        // Draws wire spheres in the editor so you can easily visualize the zones
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.grey;
            Gizmos.DrawWireSphere(transform.position, deadZoneRadius);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, CurrentWaveRadius);
        }
    }
}