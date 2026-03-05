using QuestPlugin.Runtime.Core;
using UnityEngine;

namespace QuestPlugin.Runtime.Diagnostics
{
    /// <summary>
    /// Development utility that sets a named variable on the QuestRunner when a
    /// configurable key is pressed. Useful for simulating quest triggers in the editor
    /// without wiring up full gameplay systems — for example, pressing E to mark
    /// "HasKey = true" and test whether a downstream condition node responds correctly.
    /// </summary>
    public class QuestInputVarSetter : MonoBehaviour
    {
        [SerializeField] private QuestRunner runner;
        [SerializeField] private string  key = "HasKey";       // Variable name to set in the quest context
        [SerializeField] private KeyCode keyCode = KeyCode.E;      // Key that triggers the assignment
        [SerializeField] private bool    value   = true;           // Value to assign when the key is pressed

        private void Awake() => Debug.Log("QuestInputVarSetter: Awake");
        private void Start() => Debug.Log("QuestInputVarSetter: Start");

        private void Update()
        {
            if (!Input.GetKeyDown(keyCode)) return;

            Debug.Log($"QuestInputVarSetter: pressed {keyCode}");

            if (runner == null)
            {
                Debug.LogWarning("QuestInputVarSetter: runner is null.");
                return;
            }

            runner.SetVariable(key, value);
            Debug.Log($"QuestInputVarSetter: Set {key} = {value}");
        }
    }
}