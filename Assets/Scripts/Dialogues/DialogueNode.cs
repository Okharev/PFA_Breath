using System.Collections.Generic;
using UnityEngine;

namespace Dialogues
{
    [CreateAssetMenu(fileName = "NewDialogueNode", menuName = "Dialogue/Node")]
    public class DialogueNode : ScriptableObject
    {
        [Header("Node Data")] public Speaker speaker;

        [TextArea(3, 5)] public string text;

        [Header("Flow")] [Tooltip("Leave null if the dialogue ends here or branches into choices.")]
        public DialogueNode nextNode;

        [HideInInspector] public Vector2 position;

        public List<DialogueChoice> choices = new();

        [Header("Node Entry Effects")] [SerializeReference] [SubclassSelector]
        public List<IDialogueEffect> enterEffects = new();
    }
}