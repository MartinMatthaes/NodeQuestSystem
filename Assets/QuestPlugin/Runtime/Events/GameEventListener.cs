using QuestPlugin.Runtime.Core;
using UnityEngine;

namespace QuestPlugin.Runtime.Events
{
    /// <summary>
    /// MonoBehaviour bridge that forwards a GameEvent asset's raised payload to a
    /// QuestRunner's inbox. Place this in the scene alongside any GameEvent that needs
    /// to influence quest state — the listener handles subscribe/unsubscribe automatically
    /// via OnEnable/OnDisable so it is safe to enable and disable at runtime.
    /// </summary>
    public class GameEventListener : MonoBehaviour
    {
        [SerializeField] private GameEvent gameEvent;    // The event asset to listen to
        [SerializeField] private QuestRunner questRunner;  // The runner whose inbox receives the event

        private void OnEnable()
        {
            // Subscribe when enabled rather than in Start so the listener can be
            // toggled at runtime without missing events during inactive periods
            if (gameEvent != null)
                gameEvent.OnRaised += OnEventRaised;
        }

        private void OnDisable()
        {
            // Always unsubscribe to prevent callbacks firing on a disabled or destroyed listener
            if (gameEvent != null)
                gameEvent.OnRaised -= OnEventRaised;
        }

        private void OnEventRaised(GameEventPayload payload)
        {
            if (questRunner == null) return;

            // Route the event into the runner's inbox rather than processing it directly,
            // so all quests receive it on the next Update within the normal step budget
            questRunner.OnGameEventRaised(gameEvent, payload);
        }
    }
}