using System;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Conditions
{
    /// <summary>
    /// A condition that always evaluates to true regardless of context.
    /// Useful for bypassing a Condition node during development, or for
    /// Sequence nodes that should always proceed without any prerequisite check.
    /// </summary>
    [Serializable]
    public class AlwaysTrueCondition : QuestCondition
    {
        public override bool Evaluate(QuestContext ctx) => true;

        public override string GetEditorSummary() => "Condition: TRUE";
    }
}