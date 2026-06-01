using System.Collections.Generic;
using UnityEngine;

namespace Ability.NewAbilitySystem
{
    public class IntentDrawer : MonoBehaviour
    {
        [Header("Prefabs")] public LineRenderer linePrefab;

        public GameObject decalProjectorPrefab; // URP/HDRP Decal Projector
        private readonly List<GameObject> activeDecals = new();

        // Object Pools
        private readonly List<LineRenderer> activeLines = new();
        public static IntentDrawer Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        ///     Clears all currently drawn intents. Call this when Turn Execution begins.
        /// </summary>
        public void ClearAll()
        {
            // Safely check if the line still exists before asking for its gameObject
            foreach (LineRenderer line in activeLines)
                if (line != null)
                    Destroy(line.gameObject);

            // Safely check if the decal still exists before destroying
            foreach (GameObject decal in activeDecals)
                if (decal != null)
                    Destroy(decal);

            activeLines.Clear();
            activeDecals.Clear();
        }

        // --- DRAWING APIs EXPOSED TO EFFECTS ---

        public void DrawLine(Vector3 start, Vector3 end, Color color)
        {
            LineRenderer line = Instantiate(linePrefab, transform);
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);

            // --- THE FIX: Force the thickness in code ---
            line.startWidth = 0.05f;
            line.endWidth = 0.05f;

            line.startColor = color;
            line.endColor = color;
            activeLines.Add(line);
        }

        public void DrawCone(Vector3 origin, Vector3 direction, float angle, float range, Color color)
        {
            // Calculate left and right boundaries of the spread
            Vector3 leftEdge = Quaternion.Euler(0, -angle / 2f, 0) * direction * range;
            Vector3 rightEdge = Quaternion.Euler(0, angle / 2f, 0) * direction * range;

            LineRenderer line = Instantiate(linePrefab, transform);
            line.positionCount = 4; // Origin -> Left -> Right -> Origin
            line.SetPosition(0, origin);
            line.SetPosition(1, origin + leftEdge);
            line.SetPosition(2, origin + rightEdge);
            line.SetPosition(3, origin);

            // --- THE FIX: Force the thickness in code ---
            line.startWidth = 0.05f;
            line.endWidth = 0.05f;

            line.startColor = color;
            line.endColor = color;
            activeLines.Add(line);
        }

        public void DrawCircle(Vector3 center, float radius, Color color)
        {
            // 1. Spawn the Decal facing downwards
            GameObject decalObj = Instantiate(decalProjectorPrefab, center + Vector3.up * 5f,
                Quaternion.Euler(90, 0, 0), transform);

            // 2. Scale the Decal. 
            // X and Y are the width/length (radius * 2). Z is the projection depth (how far down it projects).
            decalObj.transform.localScale = new Vector3(radius * 2, radius * 2, 10f);

            // 3. Performantly change the color using a MaterialPropertyBlock
            // Note: You may need 'using UnityEngine.Rendering.Universal;' at the top of your script for URP

            activeDecals.Add(decalObj);
        }
    }
}