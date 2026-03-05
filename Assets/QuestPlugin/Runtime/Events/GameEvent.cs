using System;
using UnityEngine;

namespace QuestPlugin.Runtime.Events
{
    /// <summary>
    /// ScriptableObject-based event channel that decouples event senders from listeners.
    /// Any system can raise this asset and any system can subscribe to it without either
    /// needing a direct reference to the other — they only share a reference to the asset.
    /// Used by RaiseEventAction to fire events from the quest graph, and by
    /// InboxEventCondition to detect when a specific event has been received.
    /// </summary>
    [CreateAssetMenu(menuName = "Quest/Game Event", fileName = "GameEvent")]
    public class GameEvent : ScriptableObject
    {
        // C# event rather than UnityEvent for lower overhead and type safety;
        // subscribers must unsubscribe on destroy to avoid callbacks on dead objects
        public event Action<GameEventPayload> OnRaised;

        /// <summary>Raises the event with a fully populated payload.</summary>
        public void Raise(GameEventPayload payload)
        {
            OnRaised?.Invoke(payload);
        }

        /// <summary>Raises the event with a default payload of amount 1 and no target.
        /// Convenience overload for simple notifications that carry no extra data.</summary>
        public void Raise()
        {
            Raise(new GameEventPayload(1));
        }

        /// <summary>Raises the event with a specific numeric amount and no target.
        /// Useful for damage, score, or quantity events where only the number matters.</summary>
        public void RaiseAmount(int amount)
        {
            Raise(new GameEventPayload(amount));
        }
    }
}