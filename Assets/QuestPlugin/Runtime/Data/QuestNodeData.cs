using System;
using System.Collections.Generic;
using QuestPlugin.Runtime.Core;
using UnityEngine;

namespace QuestPlugin.Runtime.Data
{
    /// <summary>
    /// Serializable data model for a single node in a quest graph asset.
    /// Stores the node's type, canvas position, display metadata, and the
    /// polymorphic condition/action payloads that define its runtime behaviour.
    /// Owned by QuestGraphData and never modified by QuestRunner at runtime.
    /// </summary>
    [Serializable]
    public class QuestNodeData
    {
        [SerializeField] private string       id;           // Stable GUID assigned once at creation
        [SerializeField] private QuestNodeType type;
        [SerializeField] private Vector2      position;     // Canvas position in world space (editor only)
        [SerializeField] private string       title;
        [SerializeField] private string       description;
        [SerializeField] private string       jsonPayload;  // Optional freeform data for runtime use by custom actions

        [SerializeField] private int      outputCount;   // Number of output ports; clamped to >= 0
        [SerializeField] private string[] outputLabels;  // Designer-assigned label per output port

        // [SerializeReference] enables polymorphic storage of any QuestCondition/QuestAction subtype
        [SerializeReference] private QuestCondition      condition;
        [SerializeReference] private List<QuestAction>   actions = new();

        public string        Id   => id;
        public QuestNodeType Type => type;

        public QuestCondition Condition
        {
            get => condition;
            set => condition = value;
        }

        // Null-coalescing assignment guards against deserialisation producing a null list
        public List<QuestAction> Actions => actions ??= new List<QuestAction>();

        public Vector2 Position
        {
            get => position;
            set => position = value;
        }

        public string Title
        {
            get => title;
            set => title = value;
        }

        public string Description
        {
            get => description;
            set => description = value;
        }

        public string JsonPayload
        {
            get => jsonPayload;
            set => jsonPayload = value;
        }

        // Clamped to zero on both get and set so callers never receive or store a negative count
        public int OutputCount
        {
            get => Mathf.Max(0, outputCount);
            set => outputCount = Mathf.Max(0, value);
        }

        /// <summary>
        /// Creates a new node with a freshly generated GUID and default output count
        /// appropriate for the given type. Output labels are initialised to empty strings
        /// and filled in by the designer via the inspector.
        /// </summary>
        public QuestNodeData(QuestNodeType type, Vector2 position, string title = "Node")
        {
            id            = Guid.NewGuid().ToString("N");
            this.type     = type;
            this.position = position;
            this.title    = title;

            description = string.Empty;
            jsonPayload = string.Empty;

            outputCount  = DefaultOutputsFor(type);
            outputLabels = new string[outputCount];

            actions ??= new List<QuestAction>();
        }

        /// <summary>
        /// Ensures outputCount and outputLabels are consistent with the node's current type
        /// and configuration. Called before rendering ports in the graph editor and before
        /// the runner processes the node. Safe to call multiple times — idempotent.
        /// Sequence nodes use their designer-configured outputCount rather than the default.
        /// </summary>
        public void EnsureOutputs()
        {
            actions ??= new List<QuestAction>();

            int desired = DefaultOutputsFor(type);

            // Sequence nodes are the only type where outputCount is freely configurable;
            // all other types have a fixed count determined by their role in the graph
            if (type == QuestNodeType.Sequence)
                desired = Mathf.Max(1, outputCount);

            outputCount = desired;

            if (outputCount <= 0)
            {
                outputLabels = Array.Empty<string>();
                return;
            }

            // Reallocate only if the array is absent or the wrong size; preserves existing labels
            if (outputLabels == null || outputLabels.Length != outputCount)
                outputLabels = new string[outputCount];
        }

        /// <summary>
        /// Returns the display label for a given output port index. Condition nodes
        /// always return "True"/"False" regardless of stored labels. Sequence nodes
        /// default to "Step N". All other types fall back to "Out N" if no custom
        /// label has been set by the designer.
        /// </summary>
        public string GetOutputLabel(int index)
        {
            if (index < 0) return string.Empty;

            // Condition branch labels are fixed by convention and not designer-configurable
            if (type == QuestNodeType.Condition)
                return index == 0 ? "True" : "False";

            if (outputLabels != null && index < outputLabels.Length && !string.IsNullOrEmpty(outputLabels[index]))
                return outputLabels[index];

            if (type == QuestNodeType.Sequence)
                return $"Step {index + 1}";

            return $"Out {index + 1}";
        }

        public void SetOutputLabel(int index, string label)
        {
            if (index < 0) return;

            EnsureOutputs();

            if (outputLabels == null || index >= outputLabels.Length) return;
            outputLabels[index] = label;
        }

        /// <summary>
        /// Returns the canonical output count for a node type.
        /// End nodes have no outputs; Condition nodes always have exactly two (true/false);
        /// all other types default to one.
        /// </summary>
        private static int DefaultOutputsFor(QuestNodeType t)
        {
            return t switch
            {
                QuestNodeType.End       => 0,
                QuestNodeType.Condition => 2,
                _                       => 1
            };
        }
    }
}