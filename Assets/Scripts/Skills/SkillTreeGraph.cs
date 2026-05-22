using System.Collections.Generic;
using UnityEngine;

namespace Skills
{
    [CreateAssetMenu(fileName = "NewSkillTreeGraph", menuName = "Skill Tree/Graph Container")]
    public class SkillTreeGraph : ScriptableObject
    {
        [Tooltip("The raw list of nodes saved by the custom GraphView Editor.")]
        public List<SkillNodeData> AllNodes = new();

        // --- RUNTIME OPTIMIZATION ---
        // We don't want to loop through a list of 100 nodes every time we check a prerequisite.
        // This dictionary provides instant O(1) lookups during gameplay.
        private Dictionary<string, SkillNodeData> nodeDictionary;

        /// <summary>
        ///     Call this exactly once when the game starts (e.g., in SkillTreeManager.Awake)
        ///     to build the fast-lookup dictionary.
        /// </summary>
        public void InitializeRuntimeLookup()
        {
            nodeDictionary = new Dictionary<string, SkillNodeData>();
            foreach (SkillNodeData node in AllNodes)
                // TryAdd prevents errors if the tool accidentally saved duplicate GUIDs
                nodeDictionary.TryAdd(node.GUID, node);
        }

        /// <summary>
        ///     Instantly fetches a node by its GUID.
        /// </summary>
        public SkillNodeData GetNodeByGUID(string guid)
        {
            if (nodeDictionary == null)
            {
                Debug.LogWarning("SkillTreeGraph was queried before InitializeRuntimeLookup() was called!");
                InitializeRuntimeLookup();
            }

            return nodeDictionary.TryGetValue(guid, out SkillNodeData node) ? node : null;
        }
    }
}