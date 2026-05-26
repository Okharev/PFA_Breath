using UnityEngine;

namespace TechArtPlayground.Water
{
    /// <summary>
    /// Singleton manager to handle global water properties.
    /// Ensures that global shader variables are synced efficiently in O(1) time.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Ensure this runs before objects attempt to access it
    public class WaterSystemManager : MonoBehaviour
    {
        public static WaterSystemManager Instance { get; private set; }

        [Header("Global Water Settings")]
        [SerializeField] private Texture2D bakedIntersectionMap;
        [SerializeField] private float globalRippleSpeedMultiplier = 1.0f;

        // Cache shader property IDs to avoid string hashing overhead in loops
        private readonly int intersectionMapID = Shader.PropertyToID("_IntersectionMap");
        private readonly int globalTimeID = Shader.PropertyToID("_GlobalWaterTime");

        private void Awake()
        {
            // Standard Singleton implementation
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        
            Instance = this;
            // Optional: DontDestroyOnLoad(gameObject); if water persists across scenes
        
            InitializeWaterData();
        }

        private void InitializeWaterData()
        {
            if (bakedIntersectionMap != null)
            {
                // Pushing the texture globally means you don't need to assign it to 50 different water material chunks
                Shader.SetGlobalTexture(intersectionMapID, bakedIntersectionMap);
            }
        }

        private void Update()
        {
            // Example of driving custom time globally. 
            // This is highly useful if you want to pause the game (Time.timeScale = 0) 
            // but keep water animating, or sync it to a custom wind manager.
            float customTime = Time.time * globalRippleSpeedMultiplier;
            Shader.SetGlobalFloat(globalTimeID, customTime);
        
            // Note: You would need to replace `_Time.y` with `_GlobalWaterTime` in the HLSL shader to use this.
        }
    }
}