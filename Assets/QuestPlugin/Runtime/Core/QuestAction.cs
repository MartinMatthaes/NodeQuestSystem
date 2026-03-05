using System;

namespace QuestPlugin.Runtime.Core
{
    /// <summary>
    /// Base class for all quest actions — discrete side effects that fire when
    /// a graph node is traversed. Subclasses implement Execute to define the
    /// specific behaviour, such as modifying context variables, spawning objects,
    /// or raising events. Marked [Serializable] so concrete subclasses can be
    /// stored as [SerializeReference] fields in graph node assets.
    /// </summary>
    [Serializable]
    public abstract class QuestAction
    {
        /// <summary>
        /// Performs the action using the provided quest context.
        /// Implementations should be defensive against a null context
        /// since actions may be invoked before full initialization.
        /// </summary>
        public abstract void Execute(QuestContext ctx);
    }
}