using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace QuestPlugin.Runtime.Data
{
    /// <summary>
    /// Represents a directed edge in the quest graph, connecting one node's output
    /// port to another node's input port. Stored as an asset-serialized value so
    /// connections persist across editor sessions alongside the graph's node list.
    /// </summary>
    [Serializable]
    public class QuestConnectionData
    {
        [FormerlySerializedAs("_from")] [SerializeField] private QuestPortRef from;
        [FormerlySerializedAs("_to")]   [SerializeField] private QuestPortRef to;

        public QuestPortRef From => from;
        public QuestPortRef To   => to;

        public QuestConnectionData(QuestPortRef from, QuestPortRef to)
        {
            this.from = from;
            this.to   = to;
        }
    }
}