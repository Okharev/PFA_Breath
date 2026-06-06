using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Skills.Editor
{
    [AttributeUsage(AttributeTargets.Field)]
    public class SubclassSelectorAttribute : PropertyAttribute { }

    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // 1. Safety Check: Ensure this is used on a [SerializeReference] field
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.HelpBox(position, "[SubclassSelector] requires a [SerializeReference] field.", MessageType.Error);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            // 2. Calculate Rects with respect to indentation
            Rect indentedRect = EditorGUI.IndentedRect(position);
            Rect foldoutRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
            
            // Adjust button to fit remaining width, respecting label width
            float buttonX = position.x + EditorGUIUtility.labelWidth;
            Rect buttonRect = new Rect(buttonX, position.y, position.width - EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);

            // 3. Draw the Dropdown Button
            string typeName = property.managedReferenceValue == null
                ? "Null (Select Type)"
                : property.managedReferenceValue.GetType().Name;

            if (GUI.Button(buttonRect, new GUIContent(typeName), EditorStyles.popup))
            {
                ShowDropdown(property);
            }

            // 4. Draw the Foldout and Children, 
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded && property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;

                SerializedProperty iterator = property.Copy();
                bool enterChildren = true;
                float currentY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                while (iterator.NextVisible(enterChildren))
                {
                    if (SerializedProperty.EqualContents(iterator, property.GetEndProperty())) break;

                    float childHeight = EditorGUI.GetPropertyHeight(iterator, true);
                    Rect childRect = new Rect(position.x, currentY, position.width, childHeight);

                    EditorGUI.PropertyField(childRect, iterator, true);

                    currentY += childHeight + EditorGUIUtility.standardVerticalSpacing;
                    enterChildren = false; 
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return EditorGUIUtility.singleLineHeight; // Height for the HelpBox
            }

            float totalHeight = EditorGUIUtility.singleLineHeight;

            if (property.isExpanded && property.managedReferenceValue != null)
            {
                SerializedProperty iterator = property.Copy();
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren))
                {
                    if (SerializedProperty.EqualContents(iterator, property.GetEndProperty())) break;
                    
                    totalHeight += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
                    enterChildren = false;
                }
            }

            return totalHeight;
        }

        private static void ShowDropdown(SerializedProperty property)
        {
            GenericMenu menu = new GenericMenu();

            menu.AddItem(new GUIContent("Null"), property.managedReferenceValue == null, () => AssignType(property, null));
            menu.AddSeparator("");

            Type baseType = GetBaseType(property);
            if (baseType != null)
            {
                // Fetch derived types and filter out abstract/interface and types without parameterless constructors
                IEnumerable<Type> derivedTypes = TypeCache.GetTypesDerivedFrom(baseType)
                    .Where(t => !t.IsAbstract && !t.IsInterface && t.GetConstructor(Type.EmptyTypes) != null);

                foreach (Type type in derivedTypes)
                {
                    // Use FullName and replace '.' with '/' to create nested menus based on Namespaces
                    string menuPath = type.FullName != null ? type.FullName.Replace('.', '/') : type.Name;
                    
                    menu.AddItem(new GUIContent(menuPath), property.managedReferenceValue?.GetType() == type, () => AssignType(property, type));
                }
            }

            menu.ShowAsContext();
        }

        private static void AssignType(SerializedProperty property, Type type)
        {
            property.serializedObject.Update();
            
            try
            {
                property.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SubclassSelector] Failed to instantiate type {type?.Name}: {e.Message}");
                property.managedReferenceValue = null;
            }
            
            property.serializedObject.ApplyModifiedProperties();
        }

        private static Type GetBaseType(SerializedProperty property)
        {
            string typeName = property.managedReferenceFieldTypename;
            if (string.IsNullOrEmpty(typeName)) return null;

            string[] parts = typeName.Split(' ');
            if (parts.Length == 2) 
            {
                return Type.GetType($"{parts[1]}, {parts[0]}");
            }
            return null;
        }
    }
}