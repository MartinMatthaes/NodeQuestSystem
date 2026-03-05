using System;
using UnityEngine;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Actions
{
    /// <summary>
    /// Sets a named string variable in the quest context to a fixed value.
    /// Useful for tracking non-numeric state such as active dialogue branch,
    /// current quest stage name, or a chosen player faction.
    /// </summary>
    [Serializable]
    public class SetStringVarAction : QuestAction
    {
        [SerializeField] private string key;    // Name of the string variable to set
        [SerializeField] private string value;  // Value to assign; empty string is valid and distinct from unset

        public override void Execute(QuestContext ctx)
        {
            if (ctx == null) return;
            if (string.IsNullOrWhiteSpace(key)) return;

            ctx.SetVar(key, value);
        }
    }
}