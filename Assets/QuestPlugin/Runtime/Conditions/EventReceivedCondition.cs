using System;
using QuestPlugin.Runtime.Core;
using UnityEngine.Serialization;

namespace QuestPlugin.Runtime.Conditions
{
    /// <summary>
    /// Checks whether a named event has been received by testing a boolean flag
    /// stored in the quest context under the "event." prefix. Optionally consumes
    /// the flag on a successful evaluation so the condition only passes once.
    /// </summary>
    /// <remarks>
    /// Deprecated — use InboxEventCondition instead. This class is retained for
    /// backwards compatibility with saved assets and will be removed in a future version.
    /// [FormerlySerializedAs] attributes preserve existing serialized field data
    /// during the rename from PascalCase to camelCase field naming.
    /// </remarks>
    [Obsolete("Legacy. Use InboxEventCondition instead.")]
    [Serializable]
    public class EventReceivedCondition : QuestCondition
    {
        [FormerlySerializedAs("EventName")]
        public string eventName;

        [FormerlySerializedAs("ConsumeOnSuccess")]
        public bool consumeOnSuccess = false;  // When true, clears the flag after passing so it can only fire once

        // Namespaced key prevents collisions with other boolean variables in the same context
        private string Key => string.IsNullOrEmpty(eventName) ? null : "event." + eventName;

        public override bool Evaluate(QuestContext ctx)
        {
            if (ctx == null) return false;
            if (string.IsNullOrEmpty(eventName)) return false;

            var k   = Key;
            var seen  = ctx.GetVar<bool>(k);

            // Consuming the flag resets it to false so subsequent evaluations return false
            // until the event is raised again — useful for one-shot trigger conditions
            if (seen && consumeOnSuccess)
                ctx.SetVar(k, false);

            return seen;
        }

        public override string GetEditorSummary()
        {
            if (string.IsNullOrEmpty(eventName))
                return "event\nname: <empty>";

            return consumeOnSuccess
                ? $"event: {eventName}\nconsume: true"
                : $"event: {eventName}";
        }
    }
}