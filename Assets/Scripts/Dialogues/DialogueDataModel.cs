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
        public DialogueContext(Blackboard global, Blackboard local, Speaker speaker, GameObject instigator)
        {
            GlobalBlackboard = global;
            LocalBlackboard = local;
            CurrentSpeaker = speaker;
            Instigator = instigator;
        }

        public Blackboard GlobalBlackboard { get; private set; }
        public Blackboard LocalBlackboard { get; private set; }

        public Speaker CurrentSpeaker { get; private set; }
        public GameObject Instigator { get; private set; }
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