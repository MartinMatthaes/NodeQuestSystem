using System;
using UnityEngine;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Actions
{
    /// <summary>
    /// Adds a fixed integer delta to a named variable in the quest context.
    /// Useful for incrementing counters such as kill counts, item quantities,
    /// or score values as part of a quest action sequence.
    /// </summary>
    [Serializable]
    public class AddIntVarAction : QuestAction
    {
        [SerializeField] private string key;      // Name of the integer variable to modify
        [SerializeField] private int delta = 1;   // Amount to add

        public override void Execute(QuestContext ctx)
        {
            if (ctx == null) return;
            if (string.IsNullOrWhiteSpace(key)) return;

            var current = ctx.GetVar<int>(key);
            ctx.SetVar(key, current + delta);
        }
    }
}