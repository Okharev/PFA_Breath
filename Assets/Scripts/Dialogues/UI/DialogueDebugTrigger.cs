using Ability.NewAbilitySystem;
using System;
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

        public bool isEntered;
        public bool isPlayed = false;

        

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
            // Play conversation if the player is close enough of the zone
            // Conversation can only be played once
            //if (!isEntered) return;

            if (isPlayed) return;

            Debug.Log("[CheckpointDialogue] Dialogue clicked! Opening Conversation.");

            LaunchConversation();
        }

        public event Action onCleared;

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
            isPlayed = true;
                

            onCleared ?.Invoke();
        }


        //private void OnTriggerEnter(Collider other)
        //{
        //    if (other.gameObject.CompareTag("Player"))
        //    {
        //        isEntered = true;
        //    }
        //}

        //private void OnTriggerExit(Collider other)
        //{
        //    if (other.gameObject.CompareTag("Player"))
        //    {
        //        isEntered = false;
        //    }
        //}
    }
}