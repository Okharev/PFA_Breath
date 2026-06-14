using Ability.NewAbilitySystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dialogues.UI
{
    public class DialogueDebugDirectTrigger : MonoBehaviour, IInteractable
    {
        [Header("Debug Settings")]
        [Tooltip("The key that triggers the conversation.")]
        [SerializeField] private Key debugKey = Key.F9;
    
        [SerializeField] private Conversation debugConversation;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[debugKey].wasPressedThisFrame)
            {
                LaunchConversation();
            }
        }
#endif
    
        public void Interact(GameObject instigator)
        {
            Debug.Log("[CheckpointDialogue] Dialogue clicked! Opening Conversation.");

            LaunchConversation();

        }

        private void LaunchConversation()
        {
            if (debugConversation == null)
            {
                Debug.LogWarning("Debug Dialogue Trigger: No conversation assigned!");
                return;
            }

            if (DialogueManager.Instance == null)
            {
                Debug.LogError("DialogueManager Instance not found. Ensure it exists in the scene.");
                return;
            }

            DialogueManager.Instance.StartConversation(debugConversation, gameObject);
            Debug.Log($"Launched debug conversation: {debugConversation.conversationTitle}");
        }
    }
}