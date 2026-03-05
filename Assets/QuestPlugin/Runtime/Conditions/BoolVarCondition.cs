using System;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Conditions
{
    /// <summary>
    /// Evaluates to true when the named boolean variable in the quest context
    /// is set and holds the value true. An unset or missing key returns false
    /// rather than throwing, so the condition fails gracefully before the
    /// variable has been initialized by a SetBoolVarAction upstream.
    /// </summary>
    [Serializable]
    public class BoolVarCondition : QuestCondition
    {
        public string key;  // Name of the boolean context variable to test

        public override bool Evaluate(QuestContext ctx)
        {
            // GetVar defaults to false for missing keys, so an uninitialised variable
            // will always take the false branch rather than causing an error
            return !string.IsNullOrWhiteSpace(key) && ctx.GetVar(key, false);
        }

        public override string GetEditorSummary()
        {
            return string.IsNullOrWhiteSpace(key)
                ? "bool var\nkey: <empty>"
                : $"bool var\nkey: {key}\n== true";
        }
    }
}