using System;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Actions
{
    /// <summary>
    /// Marks the active quest as complete by locating the scene's QuestRunner
    /// and calling its Complete method. Intended as a terminal action placed on
    /// End nodes to formally close out a quest when the graph reaches that point.
    /// </summary>
    [Serializable]
    public class CompleteQuestAction : QuestAction
    {
        public override void Execute(QuestContext ctx)
        {
            var runner = UnityEngine.Object.FindFirstObjectByType<QuestRunner>();
            if (runner != null)
            {
                runner.Complete();
            }
        }
    }
}