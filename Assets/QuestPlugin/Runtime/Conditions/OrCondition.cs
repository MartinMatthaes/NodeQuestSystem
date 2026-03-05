using System;
using System.Collections.Generic;
using System.Linq;
using QuestPlugin.Runtime.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace QuestPlugin.Runtime.Conditions
{
    /// <summary>
    /// A composite condition that passes when at least one child condition evaluates
    /// to true — a logical OR gate. An empty condition list evaluates to false,
    /// consistent with the mathematical convention that a disjunction over an
    /// empty set is vacuously false (contrast with AndCondition's empty-list true).
    /// </summary>
    [Serializable]
    public class OrCondition : QuestCondition
    {
        // [SerializeReference] allows the list to hold any QuestCondition subtype,
        // enabling composite conditions such as OR(AND(...), AND(...))
        [FormerlySerializedAs("Conditions")]
        [SerializeReference]
        public List<QuestCondition> conditions = new();

        public override bool Evaluate(QuestContext ctx)
        {
            if (ctx == null) return false;

            // Empty list short-circuits to false — a disjunction with no terms cannot be satisfied
            if (conditions == null || conditions.Count == 0) return false;

            // Null entries are skipped so partially configured lists don't cause errors;
            // if all entries are null, Any() returns false, which is the correct behaviour
            return conditions.Where(c => c != null).Any(c => c.Evaluate(ctx));
        }

        public override string GetEditorSummary()
        {
            if (conditions == null || conditions.Count == 0)
                return "OR\nconditions: 0";

            var valid = conditions.Where(c => c != null).ToList();
            var count = valid.Count;

            switch (count)
            {
                case 0:
                    return "OR\nconditions: 0";

                case <= 2:
                {
                    // Show each condition's first line to keep the node preview compact
                    var lines = valid
                        .Select(c => c.GetEditorSummary().Split('\n')[0])
                        .ToArray();
                    return "OR\n" + string.Join("\n", lines);
                }
            }

            // Beyond two conditions, truncate with a count so the node body doesn't overflow
            var first  = valid[0].GetEditorSummary().Split('\n')[0];
            var second = valid[1].GetEditorSummary().Split('\n')[0];
            return $"OR\n{first}\n{second}\n+{count - 2} more";
        }
    }
}