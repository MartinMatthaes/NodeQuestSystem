using System;
using QuestPlugin.Runtime.Core;
using UnityEngine.Serialization;

namespace QuestPlugin.Runtime.Conditions
{
    /// <summary>
    /// Evaluates to true if a named variable exists in the quest context, regardless
    /// of its value or type. Useful for checking whether an upstream action has run
    /// at all, rather than testing the specific value it set.
    /// </summary>
    [Serializable]
    public class VarExistsCondition : QuestCondition
    {
        [FormerlySerializedAs("Key")] public string key;  // Name of the variable to check for presence

        public override bool Evaluate(QuestContext ctx)
        {
            if (ctx == null) return false;

            // Variables dictionary may be null if no vars have been set yet on this context
            return ctx.Variables != null && ctx.Variables.ContainsKey(key);
        }

        public override string GetEditorSummary()
        {
            return string.IsNullOrWhiteSpace(key) ? "var exists <empty>" : $"exists: {key}";
        }
    }
}