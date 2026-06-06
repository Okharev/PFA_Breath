using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dialogues
{
    public class DialogueManager : MonoBehaviour
    {
        // The active state of the current conversation
        private DialogueContext _currentContext;
        private DialogueNode _currentNode;

        [Header("Global State")] private Blackboard _globalBlackboard;

        private Blackboard _localBlackboard;
        public static DialogueManager Instance { get; private set; }

        private void Awake()
        {
            // Simple Singleton setup for easy access
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Initialize the persistent Global state
            _globalBlackboard = new Blackboard();
            _globalBlackboard.RegisterModule(new DialogueMemory());
        }

        // ==========================================
        // OBSERVER EVENTS (The Core of Modularity)
        // ==========================================

        /// <summary>Fired when a conversation officially begins.</summary>
        public event Action<Conversation> OnConversationStarted;

        /// <summary>Fired when the dialogue hits a dead end and closes.</summary>
        public event Action OnConversationEnded;

        /// <summary>Fired when a new node is reached. Listen to this to update UI text/portraits.</summary>
        public event Action<DialogueNode> OnNodeEntered;

        /// <summary>Fired when the current node requires the player to make a choice.</summary>
        public event Action<List<DialogueChoice>> OnChoicesAvailable;

        // ==========================================
        // PUBLIC API (Called by Triggers/Input)
        // ==========================================

        public void StartConversation(Conversation conversation, GameObject instigator)
        {
            if (conversation == null || conversation.startingNode == null)
            {
                Debug.LogWarning("Attempted to start an empty conversation.");
                return;
            }

            // 1. Setup Local state for this specific conversation
            _localBlackboard = new Blackboard();
            _localBlackboard.RegisterModule(new DialogueMemory());

            // 2. Create the context
            _currentContext = new DialogueContext(_globalBlackboard, _localBlackboard,
                conversation.startingNode.speaker, instigator);

            // 3. Announce the start and enter the first node
            OnConversationStarted?.Invoke(conversation);
            EnterNode(conversation.startingNode);
        }

        /// <summary>
        ///     Called when the player presses the "Next" button on a standard dialogue line.
        /// </summary>
        public void Continue()
        {
            if (_currentNode == null) return;

            // Block continuing if the player is supposed to make a choice
            if (_currentNode.choices != null && _currentNode.choices.Count > 0)
            {
                Debug.LogWarning("Cannot blindly continue; a choice must be made.");
                return;
            }

            // Move to the next node, or end if there isn't one
            EnterNode(_currentNode.nextNode);
        }

        /// <summary>
        ///     Called by the UI when the player clicks a specific choice button.
        /// </summary>
        public void SelectChoice(DialogueChoice choice)
        {
            // 1. Execute any specific effects tied to this choice (e.g., spending gold)
            foreach (IDialogueEffect effect in choice.choiceEffects) effect.Execute(_currentContext);

            // 2. Move down the branch
            EnterNode(choice.nextNode);
        }

        // ==========================================
        // INTERNAL FLOW LOGIC
        // ==========================================

        private void EnterNode(DialogueNode node)
        {
            if (node == null)
            {
                EndConversation();
                return;
            }

            _currentNode = node;

            // Update the context if the speaker changed mid-conversation
            if (node.speaker != null)
                _currentContext = new DialogueContext(_globalBlackboard, _localBlackboard, node.speaker,
                    _currentContext.Instigator);

            // Execute Entry Effects (e.g., play animation, give item)
            foreach (IDialogueEffect effect in node.enterEffects) effect.Execute(_currentContext);

            // Broadcast that we have new dialogue to display
            OnNodeEntered?.Invoke(node);

            EvaluateNextSteps(node);
        }

        private void EvaluateNextSteps(DialogueNode node)
        {
            // If the node has choices, evaluate which ones the player is allowed to see
            if (node.choices != null && node.choices.Count > 0)
            {
                List<DialogueChoice> validChoices = new();

                foreach (DialogueChoice choice in node.choices)
                    if (choice.IsAvailable(_currentContext))
                        validChoices.Add(choice);

                // Broadcast the filtered list of choices to the UI
                OnChoicesAvailable?.Invoke(validChoices);
            }
        }

        private void EndConversation()
        {
            _currentNode = null;
            _currentContext = null;
            _localBlackboard = null; // Wipes the local memory clean!

            OnConversationEnded?.Invoke();
        }
    }
}