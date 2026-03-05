using System;
using UnityEngine;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Actions
{
    /// <summary>
    /// Subtracts a fixed integer delta from a named variable in the quest context.
    /// Useful for decrementing counters such as remaining lives, resource costs,
    /// or attempt limits. For absolute assignment, use SetIntVarAction instead.
    /// </summary>
    [Serializable]
    public class SubtractIntVarAction : QuestAction
    {
        [SerializeField] private string key;     // Name of the integer variable to modify
        [SerializeField] private int delta = 1;  // Amount to subtract; no floor is applied, so the result can go negative

        public override void Execute(QuestContext ctx)
        {
            if (ctx == null) return;
            if (string.IsNullOrWhiteSpace(key)) return;

            int current = ctx.GetVar<int>(key);
            ctx.SetVar(key, current - delta);
        }
    }
}