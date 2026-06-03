using System.Collections.Generic;
using UnityEngine;


namespace TechArtPlayground.Oasis
{

    [ExecuteAlways]
    public class OasisManager : MonoBehaviour
    {
        // ----------------------------------------------------
        // THE FIX: Lazy-Loaded Singleton
        // ----------------------------------------------------
        private static OasisManager _instance;
        public static OasisManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Unity 6.4 API for finding objects safely
                    _instance = Object.FindFirstObjectByType<OasisManager>();
                }
                return _instance;
            }
        }

        [Header("Global Visual Settings")]
        [Tooltip("1 = The dead zone is fully grayscale, 0 = Normal color")]
        [Range(0f, 1f)] public float deadZoneDesaturation = 1.0f;
        [ColorUsage(true, true)] public Color expansionGlowColor = new Color(0f, 2f, 1f, 1f); 
        public float glowWidth = 2.0f;

        private const int MAX_OASES = 20; 
        
        private List<OasisNode> activeOases = new List<OasisNode>();
        
        private Vector4[] oasisDataArray = new Vector4[MAX_OASES];
        private Vector4[] oasisMaxDataArray = new Vector4[MAX_OASES];

        // Property IDs
        private static readonly int OasisDataID = Shader.PropertyToID("_OasisData");
        private static readonly int OasisMaxDataID = Shader.PropertyToID("_OasisMaxData");
        private static readonly int ActiveOasisCountID = Shader.PropertyToID("_ActiveOasisCount");
        
        private static readonly int DesaturationID = Shader.PropertyToID("_DesaturationAmount"); 
        private static readonly int EdgeEmissionID = Shader.PropertyToID("_EdgeEmission");
        private static readonly int EdgeWidthID = Shader.PropertyToID("_EdgeWidth");

        private void OnEnable()
        {
            // Ensure the instance is set when enabled, just in case
            _instance = this;
        }

        public void RegisterOasis(OasisNode node)
        {
            if (!activeOases.Contains(node) && activeOases.Count < MAX_OASES) 
                activeOases.Add(node);
        }

        public void DeregisterOasis(OasisNode node)
        {
            if (activeOases.Contains(node)) 
                activeOases.Remove(node);
        }

        void Update()
        {
            // Fail-safe: don't push empty arrays if there are no oases
            if (activeOases.Count == 0)
            {
                Shader.SetGlobalInt(ActiveOasisCountID, 0);
                return;
            }

            for (int i = 0; i < activeOases.Count; i++)
            {
                Vector3 pos = activeOases[i].transform.position;
                float currentRad = activeOases[i].CurrentWaveRadius;
                float maxRad = activeOases[i].deadZoneRadius;
                
                oasisDataArray[i] = new Vector4(pos.x, pos.y, pos.z, currentRad);
                oasisMaxDataArray[i] = new Vector4(maxRad, 0, 0, 0); 
            }

            Shader.SetGlobalVectorArray(OasisDataID, oasisDataArray);
            Shader.SetGlobalVectorArray(OasisMaxDataID, oasisMaxDataArray);
            Shader.SetGlobalInt(ActiveOasisCountID, activeOases.Count);

            Shader.SetGlobalFloat(DesaturationID, deadZoneDesaturation);
            Shader.SetGlobalColor(EdgeEmissionID, expansionGlowColor);
            Shader.SetGlobalFloat(EdgeWidthID, glowWidth);
        }
        
        private void OnDestroy()
        {
            // 1. Tell the GPU to instantly stop rendering all oases
            Shader.SetGlobalInt(ActiveOasisCountID, 0);

            // 2. Clear the static Singleton reference so the next scene can cleanly assign its own
            if (_instance == this)
            {
                _instance = null;
            }
    
            // 3. Clear the list to prevent memory leaks from dangling references
            activeOases.Clear();
        }
    }
}