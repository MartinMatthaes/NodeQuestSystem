using QuestPlugin.Runtime.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Game.World.Interactables
{
    /// <summary>
    /// Destroys this GameObject when the player enters its trigger, firing a
    /// UnityEvent beforehand so the scene can respond without coupling this
    /// component to any specific gameplay system — e.g. incrementing a counter,
    /// playing a sound, or triggering a quest variable via QuestInputVarSetter.
    /// </summary>
    public class CoinPickup : MonoBehaviour
    {
        // Inspector-wired callbacks invoked on pickup; keeps this component generic
        // and reusable across different coin types without subclassing
        [SerializeField] private UnityEvent onPickup;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            Pickup();
        }

        private void Pickup()
        {
            onPickup?.Invoke();  // Notify listeners before destroying so they receive the event
            Destroy(gameObject);
        }
    }
}