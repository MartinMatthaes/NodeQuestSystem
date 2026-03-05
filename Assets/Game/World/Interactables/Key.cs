using UnityEngine;
using UnityEngine.Events;

namespace Game.World.Interactables
{
    /// <summary>
    /// Destroys this GameObject when the player enters its trigger, firing a
    /// UnityEvent beforehand so the scene can respond — typically setting a quest
    /// variable via QuestInputVarSetter to signal that the key has been collected.
    /// Functionally identical to CoinPickup but kept separate so key and coin
    /// pickups can diverge in behaviour independently as the project grows.
    /// </summary>
    public class Key : MonoBehaviour
    {
        // Inspector-wired callbacks invoked on pickup; wire to QuestInputVarSetter
        // or any other scene component that needs to know the key was collected
        [SerializeField] private UnityEvent onPickup;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            Pickup();
        }

        private void Pickup()
        {
            onPickup?.Invoke();  // Notify listeners before destroying so they can still access this object if needed
            Destroy(gameObject);
        }
    }
}