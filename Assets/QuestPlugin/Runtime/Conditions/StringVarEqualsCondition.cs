using System;
using QuestPlugin.Runtime.Core;
using UnityEngine.Serialization;

namespace QuestPlugin.Runtime.Conditions
{
    /// <summary>
    /// Compares a named string variable in the quest context against a fixed value.
    /// Case sensitivity is configurable; defaults to case-insensitive to avoid
    /// common designer errors where capitalization differences cause missed matches.
    /// </summary>
    [Serializable]
    public class StringVarEqualsCondition : QuestCondition
    {
        // [FormerlySerializedAs] attributes preserve data in existing assets
        // after the field names were lowercased to match C# naming conventions
        [FormerlySerializedAs("Key")]       public string key;
        [FormerlySerializedAs("Value")]     public string value;
        [FormerlySerializedAs("IgnoreCase")] public bool ignoreCase = true;

        public override bool Evaluate(QuestContext ctx)
        {
            if (ctx == null) return false;

            var current = ctx.GetVar<string>(key);

            // Treat a missing variable as null and compare directly so that
            // explicitly setting value to null can match an uninitialised variable
            if (current == null) return value == null;

            var comparison = ignoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            // Ordinal comparison is used rather than culture-sensitive to keep
            // behaviour consistent across different system locale settings
            return string.Equals(current, value, comparison);
        }

        public override string GetEditorSummary()
        {
            if (string.IsNullOrWhiteSpace(key))
                return "string var\nkey: <empty>";

            // "(case)" suffix in the summary signals to designers that exact casing is required
            var cmp = ignoreCase ? "==" : "== (case)";
            return $"{key} {cmp} \"{value}\"";
        }
    }
}