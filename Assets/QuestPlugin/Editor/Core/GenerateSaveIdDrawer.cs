using System;
using UnityEditor;
using UnityEngine;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Editor.Core
{
    /// <summary>
    /// Custom property drawer for fields marked with <see cref="GenerateSaveIdAttribute"/>.
    /// Augments the default string field with a right-click context menu that lets
    /// designers generate a UUID v4, reset to a known default, or clear the value —
    /// without needing to type or paste IDs by hand.
    /// </summary>
    [CustomPropertyDrawer(typeof(GenerateSaveIdAttribute))]
    public class GenerateSaveIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Gracefully fall back to the default drawer if the attribute is accidentally
            // applied to a non-string field, so the inspector doesn't silently break
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var e = Event.current;

            // Intercept right-clicks within the field's rect to show the context menu.
            // EventType.MouseDown is checked rather than ContextClick for cross-platform
            // consistency in the Unity editor (ContextClick fires later and can conflict
            // with other editor controls on some OS/input configurations)
            if (e.type == EventType.MouseDown && e.button == 1 && position.Contains(e.mousePosition))
            {
                var menu = new GenericMenu();

                // Generates a UUID v4 in the standard 8-4-4-4-12 hyphenated format ("D")
                menu.AddItem(new GUIContent("Generate Save Id (UUID v4)"), false, () =>
                {
                    property.serializedObject.Update();
                    property.stringValue = Guid.NewGuid().ToString("D");
                    property.serializedObject.ApplyModifiedProperties();
                });

                menu.AddSeparator("");

                // "default" acts as a sentinel recognized by the runtime save system
                // to indicate this asset intentionally shares the global default slot
                menu.AddItem(new GUIContent("Set to default"), false, () =>
                {
                    property.serializedObject.Update();
                    property.stringValue = "default";
                    property.serializedObject.ApplyModifiedProperties();
                });

                // Empty string signals an unassigned ID; the runtime can treat this
                // as an error or skip persistence depending on context
                menu.AddItem(new GUIContent("Clear"), false, () =>
                {
                    property.serializedObject.Update();
                    property.stringValue = "";
                    property.serializedObject.ApplyModifiedProperties();
                });

                menu.ShowAsContext();
                e.Use();  // Consume the event so nothing else reacts to this right-click
            }

            // Always draw the standard field so the value remains directly editable
            EditorGUI.PropertyField(position, property, label);
        }
    }
}