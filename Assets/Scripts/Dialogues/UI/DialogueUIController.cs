using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Your dialogue namespace

namespace Dialogues.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class DialogueUIController : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private VisualElement _root;
    
        // UI Elements
        private VisualElement _speakersContainer;
        private VisualElement _dialogueBox;
        private VisualElement _choicesBox;
        private Label _speakerName;
        private Label _dialogueText;
        private Button _nextButton;

        // State Tracking
        private Dictionary<Speaker, VisualElement> _speakerPortraits = new();
        
        public static bool IsDialogueOpen { get; private set; }

        private VisualElement _dialogueTextContainer;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            _root = _uiDocument.rootVisualElement;

            _speakersContainer = _root.Q<VisualElement>("SpeakersContainer");
            _dialogueBox = _root.Q<VisualElement>("DialogueBox");
            _choicesBox = _root.Q<VisualElement>("ChoicesBox");
            _speakerName = _root.Q<Label>("SpeakerName");
        
            // We now only query the container as a VisualElement
            _dialogueTextContainer = _root.Q<VisualElement>("DialogueText");
        
            _nextButton = _root.Q<Button>("NextButton");
            _nextButton.clicked += OnNextButtonClicked;

            _root.style.display = DisplayStyle.None;
        }

        private void Start()
        {
            // Subscribe to the existing DialogueManager Observers
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.OnConversationStarted += HandleConversationStarted;
                DialogueManager.Instance.OnNodeEntered += HandleNodeEntered;
                DialogueManager.Instance.OnChoicesAvailable += HandleChoicesAvailable;
                DialogueManager.Instance.OnConversationEnded += HandleConversationEnded;
            }
            else
            {
                Debug.LogError("DialogueManager Instance is missing! Ensure it initializes before the UI.");
            }
        }

        private void OnDestroy()
        {
            // Always unsubscribe to prevent memory leaks!
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.OnConversationStarted -= HandleConversationStarted;
                DialogueManager.Instance.OnNodeEntered -= HandleNodeEntered;
                DialogueManager.Instance.OnChoicesAvailable -= HandleChoicesAvailable;
                DialogueManager.Instance.OnConversationEnded -= HandleConversationEnded;
            }
        }

        // ==========================================
        // OBSERVER EVENT HANDLERS
        // ==========================================
        private void HandleConversationStarted(Conversation c) {
            IsDialogueOpen = true;
            InitializeSpeakers(c);
            _root.style.display = DisplayStyle.Flex;
        }

        private void HandleConversationEnded() {
            IsDialogueOpen = false;
            _root.style.display = DisplayStyle.None;
        }

        private void HandleNodeEntered(DialogueNode node)
        {
            _dialogueBox.style.display = DisplayStyle.Flex;
            _choicesBox.style.display = DisplayStyle.None;

            // USE THE PARSER INSTEAD OF DIRECT ASSIGNMENT
            DialogueTextParser.BuildRichText(_dialogueTextContainer, node.text);


            // Update Speaker UI
            if (node.speaker != null)
            {
                _speakerName.text = node.speaker.speakerName;
                _speakerName.style.color = node.speaker.speakerColor;
                
                // CHANGE: Schedule the highlight for the next UI tick so the animation plays
                _root.schedule.Execute(() => HighlightActiveSpeaker(node.speaker)).StartingIn(10);
            }
        
            // Hide "Next" button if choices are coming up (handled by OnChoicesAvailable)
            _nextButton.style.display = (node.choices != null && node.choices.Count > 0) 
                ? DisplayStyle.None 
                : DisplayStyle.Flex;
        }

        private void HandleChoicesAvailable(List<DialogueChoice> choices)
        {
            // Swap visibility
            _dialogueBox.style.display = DisplayStyle.None;
            _choicesBox.style.display = DisplayStyle.Flex;

            // Clear old choices
            _choicesBox.Clear();

            // Dynamically instantiate choice buttons
            foreach (DialogueChoice choice in choices)
            {
                Button choiceBtn = new Button();
                choiceBtn.text = choice.choiceText;
                choiceBtn.AddToClassList("choice-button");
            
                // Capture the choice variable to avoid closure issues in the loop
                DialogueChoice capturedChoice = choice;
                choiceBtn.clicked += () => DialogueManager.Instance.SelectChoice(capturedChoice);
            
                _choicesBox.Add(choiceBtn);
            }
        }
        

        private static void OnNextButtonClicked()
        {
            DialogueManager.Instance.Continue();
        }

        // ==========================================
        // ALGORITHMS & UI LOGIC
        // ==========================================

        /// <summary>
        /// Traverse the dialogue graph (BFS) to find all unique speakers.
        /// Time Complexity: O(V + E) 
        /// </summary>
        private void InitializeSpeakers(Conversation conversation)
        {
            _speakersContainer.Clear();
            _speakerPortraits.Clear();

            if (conversation.startingNode == null) return;

            HashSet<Speaker> uniqueSpeakers = new HashSet<Speaker>();
            HashSet<DialogueNode> visitedNodes = new HashSet<DialogueNode>();
            Queue<DialogueNode> queue = new Queue<DialogueNode>();

            queue.Enqueue(conversation.startingNode);

            // BFS Traversal
            while (queue.Count > 0)
            {
                DialogueNode currentNode = queue.Dequeue();

                // Prevent infinite loops from cyclic dialogue graphs
                if (visitedNodes.Contains(currentNode)) continue;
                visitedNodes.Add(currentNode);

                if (currentNode.speaker != null)
                {
                    uniqueSpeakers.Add(currentNode.speaker);
                }

                // Enqueue standard next node
                if (currentNode.nextNode != null)
                {
                    queue.Enqueue(currentNode.nextNode);
                }

                // Enqueue all choice branches
                if (currentNode.choices != null)
                {
                    foreach (DialogueChoice choice in currentNode.choices)
                    {
                        if (choice.nextNode != null) queue.Enqueue(choice.nextNode);
                    }
                }
            }

            // Instantiate visual elements for each discovered speaker
            foreach (Speaker speaker in uniqueSpeakers)
            {
                VisualElement portrait = new VisualElement();
                portrait.AddToClassList("speaker-portrait");
                
                // CHANGE: Spawn them in the hidden state!
                portrait.AddToClassList("speaker-hidden"); 

                if (speaker.portrait != null)
                {
                    portrait.style.backgroundImage = new StyleBackground(speaker.portrait);
                }

                _speakersContainer.Add(portrait);
                _speakerPortraits.Add(speaker, portrait);
            }
        }

        /// <summary>
        /// Manages the CSS classes to scale up the active speaker and grey out the others.
        /// </summary>
        private void HighlightActiveSpeaker(Speaker activeSpeaker)
        {
            foreach (KeyValuePair<Speaker, VisualElement> kvp in _speakerPortraits)
            {
                Speaker speaker = kvp.Key;
                VisualElement element = kvp.Value;

                // CRITICAL: Always remove the hidden class first so it can animate
                element.RemoveFromClassList("speaker-hidden");

                if (speaker == activeSpeaker)
                {
                    element.RemoveFromClassList("speaker-inactive");
                    element.AddToClassList("speaker-active");
                }
                else
                {
                    element.RemoveFromClassList("speaker-active");
                    element.AddToClassList("speaker-inactive");
                }
            }
        }
    }
}