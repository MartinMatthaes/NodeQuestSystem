using System;
using QuestPlugin.Runtime.Core;
using UnityEngine.Serialization;
using UnityEngine;

namespace QuestPlugin.Runtime.Conditions
{
    /// <summary>
    /// Wraps a single child condition and inverts its result — a logical NOT gate.
    /// A null child evaluates to true (the inverse of the conventional false default)
    /// so that an unconfigured InvertCondition fails open rather than silently blocking
    /// graph traversal during development.
    /// </summary>
    [Serializable]
    public class InvertCondition : QuestCondition
    {
        // [SerializeReference] allows the child to be any QuestCondition subtype,
        // including other composites such as AndCondition or OrCondition
        [FormerlySerializedAs("Condition")]
        [SerializeReference]
        public QuestCondition condition;

        public override bool Evaluate(QuestContext ctx)
        {
            if (condition == null) return true;  // Null child treated as false, so NOT false == true
            return !condition.Evaluate(ctx);
        }

        public override string GetEditorSummary()
        {
            if (condition == null)
                return "NOT\n<none>";

            // Only show the child's first line to keep the node preview compact
            var firstLine = condition.GetEditorSummary().Split('\n')[0];
            return $"NOT\n{firstLine}";
        }
    }
}