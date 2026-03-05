using System;
using UnityEngine;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Actions
{
    /// <summary>
    /// Sets a named integer variable in the quest context to a fixed value,
    /// overwriting whatever was previously stored. Use this to initialise a
    /// counter to zero, cap a value at a known limit, or reset state between
    /// quest stages. For relative changes, use AddIntVarAction instead.
    /// </summary>
    [Serializable]
    public class SetIntVarAction : QuestAction
    {
        [SerializeField] private string key;   // Name of the integer variable to set
        [SerializeField] private int value;    // Absolute value to assign, regardless of current state

        public override void Execute(QuestContext ctx)
        {
            if (ctx == null) return;
            if (string.IsNullOrWhiteSpace(key)) return;

            ctx.SetVar(key, value);
        }
    }
}