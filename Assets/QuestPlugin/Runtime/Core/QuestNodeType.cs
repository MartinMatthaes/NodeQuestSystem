namespace QuestPlugin.Runtime.Core
{
    /// <summary>
    /// Defines the role of a node within a quest graph, determining how the
    /// QuestRunner interprets and executes it during traversal.
    /// </summary>
    public enum QuestNodeType
    {
        Start,      // Entry point — every graph must have exactly one
        Objective,  // Player goal with a completion condition; advances when the condition passes
        Condition,  // Binary branch — evaluates a condition and routes to true or false output
        Action,     // Executes a list of QuestActions then advances to the next node
        Sequence,   // Ordered multi-output node; advances outputs one at a time
        End,        // Terminal node — triggers quest completion when reached
        Reward      // Deprecated — use Action nodes instead; retained for asset backwards compatibility
    }
}