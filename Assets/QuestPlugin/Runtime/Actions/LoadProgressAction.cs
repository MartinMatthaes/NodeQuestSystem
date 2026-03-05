using System;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Actions
{
    /// <summary>
    /// Triggers a full progress load from persistent storage via the QuestRunner,
    /// restoring any previously saved variable state into the active quest context.
    /// Intended for use at the start of a quest graph to resume a prior session.
    /// </summary>
    [Serializable]
    public class LoadProgressAction : QuestAction
    {
        public override void Execute(QuestContext ctx)
        {
            if (ctx == null) return;
            if (ctx.Runner == null) return;

            ctx.Runner.LoadAll();
        }
    }
}