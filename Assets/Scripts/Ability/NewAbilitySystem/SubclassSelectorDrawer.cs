using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ability.NewAbilitySystem
{
    [AttributeUsage(AttributeTargets.Field)]
    public class SubclassSelectorAttribute : PropertyAttribute
    {
    }

    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 1. Calculate Rects for the layout
            Rect headerRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect foldoutRect = new(position.x, position.y, EditorGUIUtility.labelWidth,
                EditorGUIUtility.singleLineHeight);
            Rect buttonRect = new(position.x + EditorGUIUtility.labelWidth, position.y,
                position.width - EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);

            // 2. Draw the Dropdown Button
            string typeName = property.managedReferenceValue == null
                ? "Null (Select Type)"
                : property.managedReferenceValue.GetType().Name;
            if (GUI.Button(buttonRect, new GUIContent(typeName), EditorStyles.popup)) ShowDropdown(property);

            // 3. Draw the Foldout and Children
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded && property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;

                SerializedProperty iterator = property.Copy();
                bool enterChildren = true;

                // Offset the Y position for the first child
                float currentY = position.y + EditorGUIUtility.singleLineHeight +
                                 EditorGUIUtility.standardVerticalSpacing;

                while (iterator.NextVisible(enterChildren))
                {
                    // Stop if we exit the current property
                    if (SerializedProperty.EqualContents(iterator, property.GetEndProperty())) break;

                    float childHeight = EditorGUI.GetPropertyHeight(iterator, true);
                    Rect childRect = new(position.x, currentY, position.width, childHeight);

                    EditorGUI.PropertyField(childRect, iterator, true);

                    currentY += childHeight + EditorGUIUtility.standardVerticalSpacing;
                    enterChildren = false; // Only iterate direct children
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float totalHeight = EditorGUIUtility.singleLineHeight;

            if (property.isExpanded && property.managedReferenceValue != null)
            {
                SerializedProperty iterator = property.Copy();
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren))
                {
                    if (SerializedProperty.EqualContents(iterator, property.GetEndProperty())) break;
                    totalHeight += EditorGUI.GetPropertyHeight(iterator, true) +
                                   EditorGUIUtility.standardVerticalSpacing;
                    enterChildren = false;
                }
            }

            return totalHeight;
        }

        private static void ShowDropdown(SerializedProperty property)
        {
            GenericMenu menu = new();

            // Option to clear the reference
            menu.AddItem(new GUIContent("Null"), property.managedReferenceValue == null,
                () => AssignType(property, null));

            // Find the base type (Interface or Abstract Class)
            Type baseType = GetBaseType(property);
            if (baseType != null)
            {
                // Fetch all derived, non-abstract classes
                IEnumerable<Type> derivedTypes =
                    TypeCache.GetTypesDerivedFrom(baseType).Where(t => !t.IsAbstract && !t.IsInterface);

                foreach (Type type in derivedTypes)
                    menu.AddItem(new GUIContent(type.Name), property.managedReferenceValue?.GetType() == type,
                        () => AssignType(property, type));
            }

            menu.ShowAsContext();
        }

        private static void AssignType(SerializedProperty property, Type type)
        {
            property.serializedObject.Update();
            property.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
            property.serializedObject.ApplyModifiedProperties();
        }

        private static Type GetBaseType(SerializedProperty property)
        {
            string typeName = property.managedReferenceFieldTypename;
            if (string.IsNullOrEmpty(typeName)) return null;

            string[] parts = typeName.Split(' ');
            if (parts.Length == 2) return Type.GetType($"{parts[1]}, {parts[0]}");
            return null;
        }
    }
}