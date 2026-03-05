using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.World.Interactables
{
    /// <summary>
    /// Reloads the active scene when the player enters this trigger, with an optional
    /// delay to allow an exit animation or fade to complete first. Used as a simple
    /// level-reset or loop mechanism without requiring a dedicated scene manager.
    /// </summary>
    public class ExitDoorTrigger : MonoBehaviour
    {
        [SerializeField] private float delay = 0f;  // Seconds to wait before reloading; 0 reloads immediately

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            // Skip the coroutine overhead for the common case of an immediate reload
            if (delay <= 0f)
                Reload();
            else
                StartCoroutine(ReloadDelayed());
        }

        // Reloads by build index rather than name so the scene works correctly
        // even if it is renamed or its path changes in the build settings
        private static void Reload()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private System.Collections.IEnumerator ReloadDelayed()
        {
            yield return new WaitForSeconds(delay);
            Reload();
        }
    }
}