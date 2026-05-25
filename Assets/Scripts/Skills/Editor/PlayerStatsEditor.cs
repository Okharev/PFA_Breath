using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Skills.Editor
{
    // Tells Unity to replace the default Inspector for PlayerStats with this script
    [CustomEditor(typeof(PlayerStats))]
    public class PlayerStatsEditor : UnityEditor.Editor
    {
        // Forces the inspector to repaint continuously so you see changes the exact millisecond you buy a skill
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            // Draw any standard serialized fields you might add later
            base.OnInspectorGUI();

            PlayerStats playerStats = (PlayerStats)target;

            // Only show the debugger when the game is actually running
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Stat Debugger is only active in Play Mode.", MessageType.Info);
                return;
            }

            if (playerStats.StatMap == null) return;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("--- RUNTIME STAT DEBUGGER ---", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Loop through the dictionary and draw a visual box for each stat
            foreach (KeyValuePair<StatType, Stat> kvp in playerStats.StatMap)
            {
                // Create a shaded box for each stat cluster
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Stat Name (e.g., "MovementSpeed")
                EditorGUILayout.LabelField(kvp.Key.ToString(), EditorStyles.boldLabel);

                EditorGUI.indentLevel++;

                // Formatting the math outputs
                EditorGUILayout.LabelField("Base Value:", kvp.Value.BaseValue.ToString("F2"));

                // Highlight the modifier count in green if it's being actively modified
                GUIStyle modStyle = new(EditorStyles.label);
                if (kvp.Value.ModifierCount > 0) modStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                EditorGUILayout.LabelField("Active Modifiers:", kvp.Value.ModifierCount.ToString(), modStyle);

                // Show final calculated value
                EditorGUILayout.LabelField("Final Value:", kvp.Value.Value.ToString("F2"));

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }
    }
}