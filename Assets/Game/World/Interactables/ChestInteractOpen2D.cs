using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.World.Interactables
{
    /// <summary>
    /// Controls a 2D chest that plays an open animation and spawns coin prefabs
    /// when the player enters its trigger zone and presses the interact key.
    /// Can only be opened once per scene session — repeated presses are ignored
    /// after the first successful interaction.
    /// </summary>
    public class ChestInteractOpen2D : MonoBehaviour
    {
        // Pre-hashed animator parameter avoids a string lookup every time the trigger is set
        private static readonly int Open1 = Animator.StringToHash("Open");

        [SerializeField] private Animator  animator;
        [SerializeField] private GameObject coinPrefab;
        [SerializeField] private Transform  coinSpawn;
        [SerializeField] private int        coinCount      = 1;
        [SerializeField] private KeyCode    interactKey    = KeyCode.E;
        [SerializeField] private float      coinSpawnDelay = 1.2f;  // Seconds to wait before spawning coins, matching the open animation length

        private bool _playerInside;  // True while the player's collider overlaps the trigger zone
        private bool _opened;        // Latched to true on first open; prevents the chest being opened again

        // Auto-assign the animator from a child object when the component is first added in the editor
        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

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

        private void Open()
        {
            _opened = true;

            if (animator) animator.SetTrigger(Open1);

            // Delay coin spawning so the coins appear at the climax of the open animation
            StartCoroutine(SpawnCoinsAfterDelay());
        }

        private System.Collections.IEnumerator SpawnCoinsAfterDelay()
        {
            yield return new WaitForSeconds(coinSpawnDelay);

            for (int i = 0; i < coinCount; i++)
            {
                if (coinPrefab && coinSpawn)
                    Instantiate(coinPrefab, coinSpawn.position, Quaternion.identity);
            }
        }

        private void Update()
        {
            if (_opened) return;        // Already open — nothing to do
            if (!_playerInside) return; // Player must be in range to interact

            if (IsInteractPressed())
                Open();
        }

        /// <summary>
        /// Bridges the legacy KeyCode inspector field to Unity's new Input System.
        /// Only a subset of common interact keys are mapped; unmapped keys return false.
        /// If the project moves to a full Input Action asset this method can be replaced
        /// with a single action reference without changing any other code.
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