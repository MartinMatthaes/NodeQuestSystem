using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Game.World.Interactables
{
    /// <summary>
    /// A door that can only be opened when a named boolean variable is true in any
    /// active quest context. Disables its solid collider and sprite on opening so
    /// the player can walk through, and fires a UnityEvent for audio/visual feedback.
    /// The required variable is typically set by an upstream quest action node,
    /// making the door's state a direct reflection of quest progress.
    /// </summary>
    public class Door : MonoBehaviour
    {
        [SerializeField] private QuestPlugin.Runtime.Core.QuestRunner questRunner;
        [SerializeField] private string  requiredKey  = "door.canOpen";  // Quest context variable that must be true to unlock this door
        [SerializeField] private KeyCode interactKey  = KeyCode.E;
        [SerializeField] private UnityEvent onOpened;                    // Fired on open; wire up sound effects, particles, etc.

        private bool           _playerInside;
        private BoxCollider2D  _collision;   // The non-trigger collider that physically blocks the player
        private SpriteRenderer _renderer;

        private void Awake()
        {
            // The door has two BoxCollider2D components: one solid (blocks movement)
            // and one trigger (detects player proximity). Find the solid one here.
            BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
            foreach (BoxCollider2D col in colliders)
            {
                if (col.isTrigger) continue;
                _collision = col;
                break;
            }

            _renderer = GetComponent<SpriteRenderer>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            _playerInside = true;

            // Fallback: attempt to find the runner on the player if none was assigned in the inspector
            if (questRunner == null)
                questRunner = other.GetComponent<QuestPlugin.Runtime.Core.QuestRunner>();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
                _playerInside = false;
        }

        private void Update()
        {
            if (!_playerInside) return;
            if (!IsInteractPressed()) return;
            if (!CanOpenFromAnyQuest()) return;

            OpenDoor();
        }

        private void OpenDoor()
        {
            onOpened?.Invoke();

            // Disable both the collider and renderer to make the door passable and invisible —
            // a simple stand-in for a full open animation
            _collision.enabled = false;
            _renderer.enabled  = false;
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

        /// <summary>
        /// Returns true if the required variable is true in any quest context managed
        /// by the runner. Checking across all quests allows multiple parallel quest
        /// lines to independently satisfy the same door condition.
        /// </summary>
        private bool CanOpenFromAnyQuest()
        {
            if (!questRunner) return false;

            questRunner.DebugVariable(requiredKey);  // Logs the current value to the console for development visibility
            return questRunner.AnyVariableTrue(requiredKey);
        }
    }
}