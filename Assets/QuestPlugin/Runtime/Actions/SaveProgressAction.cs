using System;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Actions
{
    /// <summary>
    /// Persists the current quest context state to storage via the QuestRunner.
    /// Place this action on any node where progress should be checkpointed —
    /// after completing an objective, before a difficult sequence, and so on.
    /// </summary>
    [Serializable]
    public class SaveProgressAction : QuestAction
    {
        public override void Execute(QuestContext ctx)
        {
            if (ctx == null) return;
            if (ctx.Runner == null) return;

            ctx.Runner.SaveAll();
        }
    }
}