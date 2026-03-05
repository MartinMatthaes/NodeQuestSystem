using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Game.World.Interactables
{
    /// <summary>
    /// An interactable statue that fires a UnityEvent when the player presses the
    /// interact key within its trigger zone. The "trade" framing suggests exchanging
    /// something with the statue — the actual behaviour is entirely defined by the
    /// onTrade inspector callbacks, keeping this component generic and reusable.
    /// Note: keyPrefab and keySpawn are serialised but not currently used by this
    /// component — likely intended for a future spawn-on-trade feature.
    /// </summary>
    public class Statue : MonoBehaviour
    {
        [SerializeField] private GameObject keyPrefab;   // Reserved for future use — intended spawn target for trade output
        [SerializeField] private Transform  keySpawn;    // Reserved for future use — world position to spawn the trade result
        [SerializeField] private KeyCode    interactKey = KeyCode.E;
        [SerializeField] private UnityEvent onTrade;     // Fired when the player interacts; wire up quest variables, spawns, audio, etc.

        private bool _playerInside;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
                _playerInside = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
                _playerInside = false;
        }

        private void Update()
        {
            if (!_playerInside) return;

            if (IsInteractPressed())
                Trade();
        }

        private void Trade()
        {
            onTrade?.Invoke();
        }

        /// <summary>
        /// Bridges the legacy KeyCode inspector field to Unity's new Input System.
        /// Only a subset of common interact keys are mapped; see ChestInteractOpen2D
        /// for the same pattern and notes on migrating to a full Input Action asset.
        /// </summary>
        private bool IsInteractPressed()
        {
            if (Keyboard.current == null) return false;

            return interactKey switch
            {
                KeyCode.E          => Keyboard.current.eKey.wasPressedThisFrame,
                KeyCode.F          => Keyboard.current.fKey.wasPressedThisFrame,
                KeyCode.Space      => Keyboard.current.spaceKey.wasPressedThisFrame,
                KeyCode.Return     => Keyboard.current.enterKey.wasPressedThisFrame,
                KeyCode.LeftShift  => Keyboard.current.leftShiftKey.wasPressedThisFrame,
                KeyCode.RightShift => Keyboard.current.rightShiftKey.wasPressedThisFrame,
                _                  => false
            };
        }
    }
}