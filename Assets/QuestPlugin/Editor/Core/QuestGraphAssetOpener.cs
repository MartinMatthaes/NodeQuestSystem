using System;
using QuestPlugin.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace QuestPlugin.Editor.Core
{
    /// <summary>
    /// Custom inspector for <see cref="QuestGraphData"/> assets.
    /// Renders the default serialized fields and appends an "Open Graph" button
    /// so designers can launch the graph editor directly from the Project window
    /// without going through the Tools menu.
    /// </summary>
    [CustomEditor(typeof(QuestGraphData))]
    public class QuestGraphDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10);

            if (!GUILayout.Button("Open Graph")) return;

            var graph = (QuestGraphData)target;
            QuestGraph.QuestGraphWindow.Open(graph);
        }
    }

    /// <summary>
    /// Registers a Unity asset-open callback so that double-clicking a
    /// <see cref="QuestGraphData"/> asset in the Project window opens the
    /// Quest Graph editor instead of the default inspector-only view.
    /// </summary>
    public static class QuestGraphAssetOpener
    {
        /// <summary>
        /// Called by Unity whenever any asset is opened. Returns true to signal
        /// that the open event has been handled and Unity should not fall back
        /// to its default behaviour (opening the raw inspector).
        /// </summary>
        [UnityEditor.Callbacks.OnOpenAsset]
        [Obsolete("Obsolete")]  // Suppresses the Unity 6 OnOpenAsset deprecation warning
        public static bool OnOpenAsset(int instanceId, int line)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceId);

            // Return false for any asset type other than QuestGraphData so Unity
            // continues to handle those normally (scripts, prefabs, textures, etc.)
            if (obj is not QuestGraphData graph) return false;

            QuestGraph.QuestGraphWindow.Open(graph);
            return true;
        }
    }
}