using System;
using UnityEngine;

namespace QuestPlugin.Runtime.Data
{
    /// <summary>
    /// Identifies one end of a connection in the quest graph — a specific port
    /// on a specific node. Used in pairs by QuestConnectionData to define a
    /// directed edge: one QuestPortRef for the output (From) and one for the input (To).
    /// Defined as a struct because it is always copied by value and has no identity
    /// beyond its two fields, making heap allocation unnecessary.
    /// </summary>
    [Serializable]
    public struct QuestPortRef
    {
        [SerializeField] private string nodeId;     // GUID of the node this port belongs to
        [SerializeField] private int    portIndex;  // Zero-based index of the port on that node

        public string NodeId    => nodeId;
        public int    PortIndex => portIndex;

        public QuestPortRef(string nodeId, int portIndex)
        {
            this.nodeId    = nodeId;
            this.portIndex = portIndex;
        }
    }
}