using System;
using System.Collections.Generic;

namespace QuestPlugin.Runtime.Core
{
    /// <summary>
    /// Root save data container serialized to JSON by QuestRunner.
    /// Versioned so future schema changes can detect and migrate legacy save files.
    /// </summary>
    [Serializable]
    public class QuestRunnerSaveData
    {
        public int version = 2;                      // Increment when the save schema changes in a breaking way
        public List<QuestSaveEntry> quests = new();  // One entry per quest graph tracked by the runner
    }

    /// <summary>
    /// Serializable snapshot of a single quest runtime's state.
    /// Matched back to its QuestRuntime on load via graphId, so reordering
    /// graphs in the inspector does not corrupt existing save files.
    /// </summary>
    [Serializable]
    public class QuestSaveEntry
    {
        public string graphId;     // Stable identifier used to find the matching runtime on load
        public string graphTitle;  // Human-readable label stored for debugging; not used during load

        public string currentNodeId;  // Node the quest was on when saved
        public string lastNodeId;     // Previously visited node; used for debug display
        public bool   isFinished;

        // Return stack serialized as a flat list; reversed on load to reconstruct correct Stack order
        public List<string> returnStackSequenceNodeIds = new();

        // SequenceIndex dictionary split into parallel lists because JsonUtility cannot serialize Dictionary
        public List<string> sequenceIndexKeys   = new();
        public List<int>    sequenceIndexValues = new();

        public List<string>   enteredNodeIds = new();  // Nodes visited at least once this run
        public List<VarEntry> variables      = new();  // Full context variable state at save time
    }

    /// <summary>
    /// A single serialized context variable stored as a typed string pair.
    /// Using strings for both type and value keeps the format human-readable
    /// and avoids JsonUtility's lack of support for polymorphic object serialization.
    /// </summary>
    [Serializable]
    public class VarEntry
    {
        public string key;    // Variable name as stored in QuestContext
        public string type;   // One of: "int", "float", "bool", "string"
        public string value;  // String-encoded value; parsed back to the correct type on import
    }
}