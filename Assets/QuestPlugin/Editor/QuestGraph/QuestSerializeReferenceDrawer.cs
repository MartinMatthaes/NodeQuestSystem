using System;
using System.Linq;
using QuestPlugin.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace QuestPlugin.Editor.QuestGraph
{
    /// <summary>
    /// Custom inspector UI for fields marked with [SerializeReference], allowing designers
    /// to pick any concrete subtype of QuestCondition or QuestAction from a dropdown and
    /// have its fields drawn inline. Required because Unity's default inspector does not
    /// provide type selection for polymorphic [SerializeReference] fields out of the box.
    /// </summary>
    public static class QuestSerializeReferenceUI
    {
        /// <summary>Draws a type-picker dropdown and inline fields for a single QuestCondition reference.</summary>
        public static void DrawCondition(string label, SerializedProperty prop)
        {
            DrawPicker<QuestCondition>(label, prop);
        }

        /// <summary>Draws a type-picker dropdown and inline fields for a single QuestAction reference.</summary>
        public static void DrawAction(string label, SerializedProperty prop)
        {
            DrawPicker<QuestAction>(label, prop);
        }

        // Private — only called recursively from DrawManagedReferenceChildren for nested condition lists
        private static void DrawConditionList(string label, SerializedProperty listProp)
        {
            DrawList<QuestCondition>(label, listProp);
        }

        /// <summary>Draws a list of QuestAction references with per-element type pickers and a remove button.</summary>
        public static void DrawActionList(string label, SerializedProperty listProp)
        {
            DrawList<QuestAction>(label, listProp);
        }

        /// <summary>
        /// Generic core for a single [SerializeReference] field. Shows a popup button labelled
        /// with the current concrete type name, and draws the selected object's serialized fields
        /// indented below it. Selecting a new type replaces the reference with a fresh instance,
        /// discarding any previously configured values on the old type.
        /// </summary>
        private static void DrawPicker<TBase>(string label, SerializedProperty prop)
        {
            var currentType = prop.managedReferenceValue?.GetType();
            var currentName = currentType != null ? currentType.Name : "None";

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label(label, GUILayout.Width(110));

                // The button label shows which concrete type is currently assigned
                if (GUILayout.Button(currentName, EditorStyles.popup))
                {
                    var capturedProp = prop.Copy();
                    
                    var menu = new GenericMenu();

                    // "None" clears the reference, effectively disabling this condition/action
                    menu.AddItem(new GUIContent("None"), currentType == null, () =>
                    {
                        capturedProp.managedReferenceValue = null;
                        capturedProp.serializedObject.Update();
                        capturedProp.serializedObject.ApplyModifiedProperties();
                    });

                    // Populate with every instantiable subtype, alphabetically sorted
                    foreach (var t in GetConcreteTypes(typeof(TBase)))
                    {
                        if (t == null) continue;
                        
                        var captured = t;
                        var isOn = currentType == t;
                        menu.AddItem(new GUIContent(t.Name), isOn, () =>
                        {
                            // Activator.CreateInstance requires a public parameterless constructor,
                            // enforced by GetConcreteTypes so this call should never throw
                            prop.managedReferenceValue = Activator.CreateInstance(captured);
                            prop.serializedObject.ApplyModifiedProperties();
                        });
                    }

                    menu.ShowAsContext();
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            // Only draw child fields when a concrete type has been assigned
            if (prop.managedReferenceValue == null) return;

            EditorGUI.indentLevel++;
            DrawManagedReferenceChildren(prop);
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Draws an editable list of [SerializeReference] elements, each with its own
        /// type picker, inline fields, and a remove button. Deletions are deferred until
        /// after the loop to avoid invalidating the array indices mid-iteration.
        /// </summary>
        private static void DrawList<TBase>(string label, SerializedProperty listProp)
        {
            if (listProp is not { isArray: true })
            {
                EditorGUILayout.LabelField(label, "Missing list");
                return;
            }

            GUILayout.Label(label);

            // Track the index to remove outside the loop; modifying the array
            // inside the loop would shift subsequent indices and skip elements
            var removeAt = -1;

            for (var i = 0; i < listProp.arraySize; i++)
            {
                var el = listProp.GetArrayElementAtIndex(i);
                var elType = el.managedReferenceValue?.GetType();
                var elName = elType != null ? elType.Name : "None";

                GUILayout.BeginVertical("box");
                try
                {
                    GUILayout.BeginHorizontal();
                    try
                    {
                        GUILayout.Label($"#{i}", GUILayout.Width(28));

                        // Per-element type picker — same pattern as DrawPicker but scoped to this element
                        if (GUILayout.Button(elName, EditorStyles.popup))
                        {
                            
                            var capturedProp = el.Copy();
                            var menu = new GenericMenu();

                            menu.AddItem(new GUIContent("None"), elType == null, () =>
                            {
                                capturedProp.managedReferenceValue = null;
                                capturedProp.serializedObject.Update();
                                capturedProp.serializedObject.ApplyModifiedProperties();
                            });

                            foreach (var t in GetConcreteTypes(typeof(TBase)))
                            {
                                if (t == null) continue;

                                var captured = t;
                                var isOn = elType == t;
                                
                                menu.AddItem(new GUIContent(t.Name), isOn, () =>
                                {
                                    el.managedReferenceValue = Activator.CreateInstance(captured);
                                    el.serializedObject.ApplyModifiedProperties();
                                });
                            }

                            menu.ShowAsContext();
                        }

                        if (GUILayout.Button("X", GUILayout.Width(24)))
                            removeAt = i;
                    }
                    finally
                    {
                        GUILayout.EndHorizontal();
                    }

                    if (el.managedReferenceValue != null)
                    {
                        EditorGUI.indentLevel++;
                        DrawManagedReferenceChildren(el);
                        EditorGUI.indentLevel--;
                    }
                }
                finally
                {
                    GUILayout.EndVertical();
                }
            }

            // Apply deferred removal now that iteration is complete
            if (removeAt >= 0)
                listProp.DeleteArrayElementAtIndex(removeAt);

            // Append a new null entry; the designer picks a type via the dropdown on the next repaint
            if (GUILayout.Button("Add"))
            {
                var idx = listProp.arraySize;
                listProp.InsertArrayElementAtIndex(idx);
                var el = listProp.GetArrayElementAtIndex(idx);
                el.managedReferenceValue = null;
                listProp.serializedObject.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// Returns all types that are concrete (non-abstract, non-generic) subclasses of
        /// <paramref name="baseType"/>, have a public parameterless constructor, and are not
        /// marked [Obsolete]. Uses Unity's TypeCache for fast editor-time reflection.
        /// Results are sorted alphabetically so the dropdown is stable across recompiles.
        /// </summary>
        private static Type[] GetConcreteTypes(Type baseType)
        {
            return TypeCache.GetTypesDerivedFrom(baseType)
                .Where(t =>
                    !t.IsAbstract &&
                    !t.IsGenericType &&
                    t.GetConstructor(Type.EmptyTypes) != null &&
                    !Attribute.IsDefined(t, typeof(ObsoleteAttribute), true)
                )
                .OrderBy(t => t.Name)
                .ToArray();
        }

        /// <summary>
        /// Iterates the immediate serialized children of a managed reference property and
        /// draws each one. Fields named "conditions" or "actions" are routed to their
        /// respective list drawers to support composite types such as AndCondition or
        /// OrCondition, which hold nested lists of further conditions.
        /// </summary>
        private static void DrawManagedReferenceChildren(SerializedProperty managedRefProp)
        {
            SerializedProperty it  = managedRefProp.Copy();
            SerializedProperty end = it.GetEndProperty();

            bool enterChildren = true;

            while (it.NextVisible(enterChildren) && !SerializedProperty.EqualContents(it, end))
            {
                enterChildren = false;

                if (it.propertyPath == managedRefProp.propertyPath)
                    continue;

                if (it.isArray && it.propertyType == SerializedPropertyType.Generic && it.name == "conditions")
                {
                    DrawConditionList(it.displayName, it);
                    continue;
                }

                if (it.isArray && it.propertyType == SerializedPropertyType.Generic && it.name == "actions")
                {
                    DrawActionList(it.displayName, it);
                    continue;
                }

                // Handle singular SerializeReference condition fields (e.g. InvertCondition.condition)
                if (it.propertyType == SerializedPropertyType.ManagedReference && it.name == "condition")
                {
                    var condProp = it.Copy();
                    DrawCondition(it.displayName, condProp);
                    continue;
                }

                EditorGUILayout.PropertyField(it, true);
            }
        }
    }
}