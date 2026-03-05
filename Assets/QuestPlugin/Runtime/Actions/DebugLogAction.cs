using System;
using UnityEngine;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Actions
{
    /// <summary>
    /// Prints a fixed message to the Unity console when executed.
    /// Useful for tracing graph traversal during development without
    /// attaching a debugger or adding temporary breakpoints.
    /// </summary>
    [Serializable]
    public class DebugLogAction : QuestAction
    {
        [SerializeField] private string message;  // Text to print; supports static strings only

        public override void Execute(QuestContext ctx)
        {
            Debug.Log(message);
        }
    }
}