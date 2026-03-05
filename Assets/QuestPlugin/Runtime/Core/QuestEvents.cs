using System;

namespace QuestPlugin.Runtime.Core
{
    /// <summary>
    /// Global static event bus for quest-related notifications. Allows any system
    /// to broadcast named events without direct references to listeners, keeping
    /// quest graph logic decoupled from gameplay systems such as UI, audio, and achievements.
    /// Note: subscribers must explicitly unsubscribe when destroyed to avoid
    /// callbacks firing on dead objects.
    /// </summary>
    public static class QuestEvents
    {
        /// <summary>
        /// Payload passed to every subscriber when an event is raised.
        /// Fields are optional — populate only what is relevant to the event.
        /// </summary>
        public struct EventData
        {
            public string Name;      // Identifies the event type (e.g. "enemy_killed", "item_collected")
            public string ActorId;   // The entity that caused the event; null if not applicable
            public string TargetId;  // The entity the event acted upon; null if not applicable
            public int Amount;       // Numeric quantity associated with the event (e.g. damage, count)
        }

        // Static event — all subscribers share a single invocation list for the lifetime of the application
        private static event Action<EventData> OnEvent;

        /// <summary>
        /// Broadcasts an event to all current subscribers. Safe to call with no
        /// subscribers registered — the null-conditional prevents an empty invocation list throw.
        /// </summary>
        public static void Raise(string name, string targetId = null, int amount = 1, string actorId = null)
        {
            OnEvent?.Invoke(new EventData
            {
                Name     = name,
                ActorId  = actorId,
                TargetId = targetId,
                Amount   = amount
            });
        }

        /// <summary>Registers a handler to receive all future events.</summary>
        public static void Subscribe(Action<EventData> handler) => OnEvent += handler;

        /// <summary>
        /// Removes a previously registered handler. Always call this in OnDestroy
        /// or equivalent teardown to prevent callbacks firing on destroyed objects.
        /// </summary>
        public static void Unsubscribe(Action<EventData> handler) => OnEvent -= handler;
    }
}