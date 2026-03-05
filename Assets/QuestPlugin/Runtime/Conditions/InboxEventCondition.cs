using System;
using QuestPlugin.Runtime.Core;
using QuestPlugin.Runtime.Events;

namespace QuestPlugin.Runtime.Conditions
{
    /// <summary>
    /// Checks whether a specific GameEvent has been received into the quest runner's
    /// inbox for this quest instance. Replaces the deprecated EventReceivedCondition
    /// with a direct asset reference instead of a string event name, making
    /// connections between graph nodes and events refactor-safe and inspector-visible.
    /// </summary>
    [Serializable]
    public class InboxEventCondition : QuestCondition, IUsesGameEvent
    {
        public GameEvent eventAsset;
        public GameEvent GetEventAsset() => eventAsset;  // Satisfies IUsesGameEvent for editor tooling

        // When true, the event is removed from the inbox on a successful check so it
        // can only trigger the condition once — set to false to allow repeated evaluation
        public bool consumeOnSuccess = true;

        public override bool Evaluate(QuestContext ctx)
        {
            if (ctx == null) return false;
            if (ctx.Runner == null) return false;   // Runner required to access the per-quest inbox
            if (eventAsset == null) return false;

            // QuestIndex scopes the inbox lookup to this specific quest instance,
            // preventing events intended for one quest from satisfying another
            return ctx.Runner.HasInboxEvent(ctx.QuestIndex, eventAsset.name, consumeOnSuccess);
        }

        public override string GetEditorSummary()
        {
            if (eventAsset == null)
                return "event: <none>";

            return consumeOnSuccess
                ? $"event: {eventAsset.name}\nconsume: true"
                : $"event: {eventAsset.name}";
        }
    }
}