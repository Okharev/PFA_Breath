using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dialogues
{
    /// <summary>
    ///     A base marker interface for any subsystem that can be plugged into the dialogue.
    /// </summary>
    public interface IDialogueModule
    {
        // Optional: Add universal lifecycle methods like Initialize(), Reset(), or SaveState() here if needed.
    }

    /// <summary>
    ///     Wraps all dependencies required by dialogue strategies.
    ///     Space Complexity: O(1) references.
    /// </summary>
    public class DialogueContext
    {
        public Blackboard GlobalBlackboard { get; private set; }
        public Blackboard LocalBlackboard { get; private set; }
        
        public Speaker CurrentSpeaker { get; private set; }
        public GameObject Instigator { get; private set; }

        public DialogueContext(Blackboard global, Blackboard local, Speaker speaker, GameObject instigator)
        {
            GlobalBlackboard = global;
            LocalBlackboard = local;
            CurrentSpeaker = speaker;
            Instigator = instigator;
        }
    }

    public interface IDialogueCondition
    {
        /// <summary> Evaluates to true if the choice/node should be accessible. </summary>
        bool Evaluate(DialogueContext context);
    }

    public interface IDialogueEffect
    {
        /// <summary> Executes game logic (state changes, events, animations). </summary>
        void Execute(DialogueContext context);
    }

    [CreateAssetMenu(fileName = "NewSpeaker", menuName = "Dialogue/Speaker")]
    public class Speaker : ScriptableObject
    {
        public string speakerName;
        public Color speakerColor = Color.white;
        public Sprite portrait;
    }

    [CreateAssetMenu(fileName = "NewConversation", menuName = "Dialogue/Conversation")]
    public class Conversation : ScriptableObject
    {
        public string conversationTitle;
        public DialogueNode startingNode;
    }

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

    [Serializable]
    public class DialogueChoice
    {
        public string choiceText;
        public DialogueNode nextNode;

        [Header("Logic")] [SerializeReference] [SubclassSelector]
        public List<IDialogueCondition> conditions = new();

        [SerializeReference] [SubclassSelector]
        public List<IDialogueEffect> choiceEffects = new();

        /// <summary>
        ///     Time Complexity: O(N) where N is the number of conditions on this specific choice.
        /// </summary>
        public bool IsAvailable(DialogueContext context)
        {
            foreach (IDialogueCondition condition in conditions)
                if (!condition.Evaluate(context))
                    return false;
            return true;
        }
    }
}