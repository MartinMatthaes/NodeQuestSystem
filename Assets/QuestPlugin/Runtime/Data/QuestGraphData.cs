using System;
using System.Collections.Generic;
using QuestPlugin.Runtime.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace QuestPlugin.Runtime.Data
{
    /// <summary>
    /// ScriptableObject asset that stores the complete definition of a quest graph:
    /// its nodes, directed connections, and metadata. Acts as the source of truth
    /// for graph structure — QuestRunner reads from this asset but never writes to it
    /// at runtime, keeping the asset itself stateless and reusable across sessions.
    /// </summary>
    [CreateAssetMenu(menuName = "Quest/Quest Graph", fileName = "QuestGraph", order = 1)]
    public class QuestGraphData : ScriptableObject
    {
        [SerializeField] private string graphId;              // Stable UUID used to match save data back to this asset
        [SerializeField] private string title = "New Quest";  // Human-readable name shown in the graph editor header

        [SerializeField] private List<QuestNodeData>       nodes       = new();
        [FormerlySerializedAs("_connections")]
        [SerializeField] private List<QuestConnectionData> connections = new();

        public string GraphId => graphId;
        public string Title   => title;

        public List<QuestNodeData>       Nodes       => nodes;
        public List<QuestConnectionData> Connections => connections;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Assign a stable ID if the asset was just created or the field was accidentally cleared
            if (string.IsNullOrWhiteSpace(graphId))
                graphId = Guid.NewGuid().ToString("N");
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(graphId))
                graphId = Guid.NewGuid().ToString("N");

            // Null-coalesce in case the asset was created outside the normal asset menu flow
            nodes       ??= new List<QuestNodeData>();
            connections ??= new List<QuestConnectionData>();

            // Every new graph gets a Start node so it is immediately valid and runnable
            if (nodes.Count == 0)
                nodes.Add(new QuestNodeData(QuestNodeType.Start, new Vector2(80, 80), "Start Node"));

            // Ensure all nodes have their output port lists initialised before the editor draws them
            foreach (var t in nodes)
                t.EnsureOutputs();
        }
#endif

        /// <summary>
        /// Linear search for a node by GUID. Acceptable for typical graph sizes (tens of nodes);
        /// if graphs grow large enough to make this a bottleneck, replace with a dictionary cache.
        /// </summary>
        public QuestNodeData FindNode(string id)
        {
            foreach (var t in nodes)
                if (t.Id == id)
                    return t;

            return null;
        }
    }
}