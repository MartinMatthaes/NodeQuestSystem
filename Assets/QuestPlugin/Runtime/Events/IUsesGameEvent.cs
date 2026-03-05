namespace QuestPlugin.Runtime.Events
{
    /// <summary>
    /// Marker interface implemented by any QuestCondition or QuestAction that references
    /// a GameEvent asset. QuestRunner scans all graph nodes for this interface at startup
    /// to discover which events need to be subscribed to, avoiding the need to maintain
    /// a separate manual list of events in the inspector.
    /// </summary>
    public interface IUsesGameEvent
    {
        /// <summary>Returns the GameEvent asset this condition or action depends on.</summary>
        GameEvent GetEventAsset();
    }
}