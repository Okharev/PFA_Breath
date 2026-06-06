using System.Collections.Generic;

namespace Dialogues
{
    /// <summary>
    /// A robust memory module for the Blackboard, handling conversation or global state.
    /// Acts as a centralized Data Store using Hash Tables for optimal performance.
    /// </summary>
    public class DialogueMemory : IDialogueModule
    {
        // Internal state storage using Dictionaries (Hash Tables)
        private readonly Dictionary<string, bool> _boolState = new Dictionary<string, bool>();
        private readonly Dictionary<string, int> _intState = new Dictionary<string, int>();

        /// <summary>
        /// Writes or updates a boolean flag.
        /// Time Complexity: O(1) average case.
        /// </summary>
        public void SetBool(string key, bool value)
        {
            _boolState[key] = value;
        }

        /// <summary>
        /// Reads a boolean flag. Returns the fallback if the key does not exist.
        /// Time Complexity: O(1) average case.
        /// </summary>
        public bool GetBool(string key, bool fallback = false)
        {
            return _boolState.GetValueOrDefault(key, fallback);
        }

        /// <summary>
        /// Writes or updates an integer value.
        /// Time Complexity: O(1) average case.
        /// </summary>
        public void SetInt(string key, int value)
        {
            _intState[key] = value;
        }

        /// <summary>
        /// Reads an integer value. Returns the fallback if the key does not exist.
        /// Time Complexity: O(1) average case.
        /// </summary>
        public int GetInt(string key, int fallback = 0)
        {
            return _intState.GetValueOrDefault(key, fallback);
        }

        /// <summary>
        /// Modifies an existing integer by a specific amount. Useful for spending/gaining resources.
        /// </summary>
        public void AddToInt(string key, int amount)
        {
            int currentValue = GetInt(key, 0);
            _intState[key] = currentValue + amount;
        }

        /// <summary>
        /// Wipes the memory clean. Highly useful for object pooling or resetting Local Blackboards.
        /// </summary>
        public void Clear()
        {
            _boolState.Clear();
            _intState.Clear();
        }
    }
}