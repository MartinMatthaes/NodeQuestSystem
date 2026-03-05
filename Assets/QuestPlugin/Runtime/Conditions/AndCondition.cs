using System;
using System.Collections.Generic;
using System.Linq;
using QuestPlugin.Runtime.Core;
using UnityEngine;

namespace QuestPlugin.Runtime.Conditions
{
    /// <summary>
    /// A composite condition that passes only when every child condition evaluates
    /// to true — a logical AND gate. An empty condition list is treated as true
    /// so that partially configured nodes don't silently block graph traversal.
    /// </summary>
    [Serializable]
    public class AndCondition : QuestCondition
    {
        // [SerializeReference] allows the list to hold any QuestCondition subtype,
        // including other composite conditions for arbitrarily nested logic trees
        [SerializeReference] public List<QuestCondition> conditions = new List<QuestCondition>();

        public override bool Evaluate(QuestContext ctx)
        {
            if (ctx == null) return false;

            // Empty list short-circuits to true — consistent with the mathematical
            // convention that a conjunction over an empty set is vacuously true
            if (conditions == null || conditions.Count == 0) return true;

            // Null entries are skipped rather than treated as false so that
            // partially configured condition lists don't break evaluation
            return conditions.Where(c => c != null).All(c => c.Evaluate(ctx));
        }

        public override string GetEditorSummary()
        {
            if (conditions == null || conditions.Count == 0)
                return "AND\nconditions: 0";

            var valid = conditions.Where(c => c != null).ToList();
            var count = valid.Count;

            switch (count)
            {
                case 0:
                    return "AND\nconditions: 0";

                case <= 2:
                {
                    // Show each condition's first line to keep the node preview compact
                    var lines = valid
                        .Select(c => c.GetEditorSummary().Split('\n')[0])
                        .ToArray();
                    return "AND\n" + string.Join("\n", lines);
                }
            }

            // Beyond two conditions, truncate with a count so the node body doesn't overflow
            var first  = valid[0].GetEditorSummary().Split('\n')[0];
            var second = valid[1].GetEditorSummary().Split('\n')[0];
            return $"AND\n{first}\n{second}\n+{count - 2} more";
        }
    }
}