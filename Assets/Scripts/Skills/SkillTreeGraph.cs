using System.Collections.Generic;
using Skills.Skills;
using UnityEngine;

namespace Skills
{
    [CreateAssetMenu(fileName = "NewSkillTreeGraph", menuName = "Skill Tree/Graph Container")]
    public class SkillTreeGraph : ScriptableObject
    {
        // CRITICAL FIX: This attribute allows Unity to save polymorphic child classes in a single list!
        [SerializeReference] public List<BaseNodeData> AllNodes = new();

        private Dictionary<string, BaseNodeData> nodeDictionary;

        public void InitializeRuntimeLookup()
        {
            nodeDictionary = new Dictionary<string, BaseNodeData>();
            foreach (BaseNodeData node in AllNodes) nodeDictionary.TryAdd(node.GUID, node);
        }

        public BaseNodeData GetNodeByGUID(string guid)
        {
            if (nodeDictionary == null) InitializeRuntimeLookup();
            return nodeDictionary.GetValueOrDefault(guid);
        }
    }
}