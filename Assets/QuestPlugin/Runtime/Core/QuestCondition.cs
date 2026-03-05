using System;

namespace QuestPlugin.Runtime.Core
{
    /// <summary>
    /// Base class for all quest conditions — boolean tests evaluated during graph
    /// traversal to determine which branch a Condition node should follow.
    /// Subclasses implement Evaluate to define the specific test, such as checking
    /// context variables, inventory state, or elapsed time. Marked [Serializable]
    /// so concrete subclasses can be stored as [SerializeReference] fields in graph node assets.
    /// </summary>
    [Serializable]
    public abstract class QuestCondition
    {
        /// <summary>
        /// Evaluates the condition against the provided quest context.
        /// Implementations should be defensive against a null context
        /// since conditions may be evaluated before full initialization.
        /// </summary>
        public abstract bool Evaluate(QuestContext ctx);

        /// <summary>
        /// Returns a short human-readable description of this condition for display
        /// in the graph node body. Defaults to the class name so that new subclasses
        /// are immediately identifiable in the editor without requiring an override.
        /// </summary>
        public virtual string GetEditorSummary()
        {
            return GetType().Name;
        }
    }
}