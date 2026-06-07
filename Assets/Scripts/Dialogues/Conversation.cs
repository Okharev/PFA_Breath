using UnityEngine;

namespace Dialogues
{
    [CreateAssetMenu(fileName = "NewConversation", menuName = "Dialogue/Conversation")]
    public class Conversation : ScriptableObject
    {
        public string conversationTitle;
        public DialogueNode startingNode;
    }
}