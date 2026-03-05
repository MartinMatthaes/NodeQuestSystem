using System;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Actions
{
    /// <summary>
    /// Sets a named boolean variable in the quest context to a fixed value.
    /// Commonly used to flag quest states such as "has_talked_to_npc" or
    /// "door_unlocked" that Condition nodes can then branch on.
    /// </summary>
    [Serializable]
    public class SetBoolVarAction : QuestAction
    {
        public string key;          // Name of the boolean variable to set
        public bool value = true;   // Value to assign; false can be used to explicitly reset a flag

        public override void Execute(QuestContext ctx)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            ctx?.SetVar(key, value);
        }
    }
}