using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dialogues.UI
{
    public static class DialogueTextParser
    {
        // Regex matches anything inside < > brackets
        private const string TagPattern = @"(<.*?>)";

        /// <summary>
        /// A state-machine lexer that preserves native Rich Text tags across spaces.
        /// Time Complexity: O(N) where N is the number of tokens in the string.
        /// </summary>
        public static void BuildRichText(VisualElement container, string rawText)
        {
            container.Clear();

            // Split the text, separating HTML tags from standard text
            string[] tokens = Regex.Split(rawText, TagPattern);
            
            bool isTrembling = false;
            
            // Maintains a list of currently active Rich Text tags (e.g., "<color=#ff0000>", "<b>")
            List<string> activeTags = new List<string>();

            foreach (string token in tokens)
            {
                if (string.IsNullOrEmpty(token)) continue;

                // 1. STATE MACHINE: Process Tags
                if (token.StartsWith("<") && token.EndsWith(">"))
                {
                    string lowerToken = token.ToLower();

                    // Handle Custom Effects
                    if (lowerToken == "<tremble>") { isTrembling = true; continue; }
                    if (lowerToken == "</tremble>") { isTrembling = false; continue; }

                    // Handle Standard Unity Rich Text
                    if (lowerToken.StartsWith("</"))
                    {
                        // Closing tag: Remove the last active tag
                        if (activeTags.Count > 0) activeTags.RemoveAt(activeTags.Count - 1);
                    }
                    else
                    {
                        // Opening tag: Add to our active list
                        activeTags.Add(token);
                    }
                    continue; // Move to the next token
                }

                // 2. TEXT GENERATION: Process standard words
                string[] words = token.Split(' ');
                foreach (string word in words)
                {
                    if (string.IsNullOrEmpty(word)) continue;

                    // Reconstruct the word, wrapped perfectly in all active tags
                    string formattedWord = word;
                    
                    // Prepend opening tags
                    foreach (string tag in activeTags)
                    {
                        formattedWord = tag + formattedWord;
                    }
                    
                    // Append closing tags (in reverse order for correct HTML nesting)
                    for (int i = activeTags.Count - 1; i >= 0; i--)
                    {
                        string tag = activeTags[i];
                        string tagName = GetTagName(tag);
                        formattedWord += "</" + tagName + ">";
                    }

                    // Instantiate UI Element
                    Label wordLabel = new Label(formattedWord);
                    wordLabel.AddToClassList("dialogue-word");
                    wordLabel.enableRichText = true;

                    // Apply custom C# effects
                    if (isTrembling) ApplyTrembleEffect(wordLabel);

                    container.Add(wordLabel);
                }
            }
        }

        /// <summary> Extracts the tag name (e.g., from "<color=#fff>" it returns "color") </summary>
        private static string GetTagName(string tag)
        {
            string content = tag.Trim('<', '>');
            int equalsIndex = content.IndexOf('=');
            return equalsIndex > 0 ? content.Substring(0, equalsIndex) : content;
        }

        private static void ApplyTrembleEffect(VisualElement element)
        {
            element.schedule.Execute(() =>
            {
                float offsetX = Random.Range(-2f, 2f);
                float offsetY = Random.Range(-2f, 2f);
                element.style.translate = new StyleTranslate(new Translate(offsetX, offsetY));
            }).Every(50);
        }
    }
}