using QuestPlugin.Runtime.Core;
using QuestPlugin.Runtime.Events;
using UnityEngine;

namespace QuestPlugin.Runtime.Actions
{
    /// <summary>
    /// Raises a GameEvent asset with a configured payload when executed.
    /// Decouples quest graph logic from gameplay systems.
    /// </summary>
    [System.Serializable]
    public class RaiseEventAction : QuestAction, IUsesGameEvent
    {
        [SerializeField] private GameEvent eventAsset;  // The ScriptableObject event to raise
        public GameEvent GetEventAsset() => eventAsset; // Satisfies IUsesGameEvent for editor tooling

        [SerializeField] private int amount = 1;        // Numeric payload value (e.g. score delta, quantity)
        [SerializeField] private string targetId;       // Optional identifier for the payload target entity
        

        public override void Execute(QuestContext ctx)
        {
            if (eventAsset == null) return;

            var payload = new GameEventPayload(
                amount,
                targetId: targetId
            );

            eventAsset.Raise(payload);
        }
    }
}