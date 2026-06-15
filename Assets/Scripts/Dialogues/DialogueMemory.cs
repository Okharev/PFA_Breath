using System.Collections.Generic;
using Skills;
using TechArtPlayground.Wind;
using UnityEngine;

namespace Dialogues
{
    /// <summary>
    ///     Tracks choices made within a specific context (Local or Global).
    ///     Space Complexity: O(N) where N is the number of choices made.
    /// </summary>
    public class ChoiceHistoryModule : IDialogueModule
    {
        private readonly HashSet<string> _madeChoices = new();

        /// <summary> Time Complexity: O(1) </summary>
        public void RecordChoice(string choiceId)
        {
            _madeChoices.Add(choiceId);
        }

        /// <summary> Time Complexity: O(1) </summary>
        public bool HasMadeChoice(string choiceId)
        {
            return _madeChoices.Contains(choiceId);
        }
    }

    /// <summary>
    ///     Executes game logic to record that a choice was selected.
    /// </summary>
    public class RecordLocalChoiceEffect : IDialogueEffect
    {
        [Tooltip("A unique ID for this choice within the current conversation.")]
        public string choiceId;

        public void Execute(DialogueContext context)
        {
            // Retrieve our module from the Local Blackboard
            ChoiceHistoryModule history = context.LocalBlackboard.GetModule<ChoiceHistoryModule>();
            history?.RecordChoice(choiceId);
        }
    }
    
    
    /// <summary>
    ///     Executes game logic to grant the player a specific amount of Emotion Points.
    ///     O(1) Time Complexity.
    /// </summary>
    [System.Serializable]
    public class GrantEmotionPointsEffect : IDialogueEffect
    {
        [Tooltip("The type of emotion points to grant the player.")]
        public EmotionType emotionType;

        [Tooltip("The quantity of points to grant. Can be negative to remove points.")]
        public int amount;

        public void Execute(DialogueContext context)
        {
            // Safety check to ensure the Singleton exists before attempting to modify it
            if (SkillTreeManager.Instance != null)
            {
                SkillTreeManager.Instance.AddEmotionPoints(emotionType, amount);
                Debug.Log($"[Dialogue Effect] Granted {amount} {emotionType} points to the player.");
            }
            else
            {
                Debug.LogError("[Dialogue Effect] Failed to grant points. SkillTreeManager.Instance is null! Ensure it exists in the scene.");
            }
        }
    }
    
    /// <summary>
    /// Executes a command to transition global weather during dialogue.
    /// Time Complexity: O(1) invocation. The actual transition runs asynchronously via Coroutine.
    /// Space Complexity: O(1).
    /// </summary>
    [System.Serializable]
    public class SetWeatherBlendEffect : IDialogueEffect
    {
        [Tooltip("The target weather blend percentage (0 = Calm, 1 = Tempest).")]
        [Range(0f, 1f)]
        public float targetBlend;

        [Tooltip("How long the transition should take in seconds.")]
        [Min(0f)]
        public float transitionDuration = 2f;

        public void Execute(DialogueContext context)
        {
            // Safety check: Ensure the Singleton is alive before invoking
            if (GlobalWeatherManager.Instance != null)
            {
                GlobalWeatherManager.Instance.TransitionToBlend(targetBlend, transitionDuration);
                Debug.Log($"[Dialogue Effect] Weather transitioning to {targetBlend * 100}% over {transitionDuration}s.");
            }
            else
            {
                Debug.LogWarning("[Dialogue Effect] GlobalWeatherManager is missing from the scene!");
            }
        }
    }

    /// <summary>
    ///     Evaluates to true if the specified choice was previously made.
    /// </summary>
    public class RequirePreviousChoiceCondition : IDialogueCondition
    {
        [Tooltip("The ID of the choice that must have been made.")]
        public string requiredChoiceId;

        [Tooltip("If true, the condition passes ONLY if the player DID NOT make this choice.")]
        public bool invertCheck = false;

        public bool Evaluate(DialogueContext context)
        {
            ChoiceHistoryModule history = context.LocalBlackboard.GetModule<ChoiceHistoryModule>();

            // Failsafe if the module is missing
            if (history == null) return false;

            bool hasMadeChoice = history.HasMadeChoice(requiredChoiceId);

            // Return inverted logic if requested, otherwise standard logic
            return invertCheck ? !hasMadeChoice : hasMadeChoice;
        }
    }
}