using System.Collections.Generic;
using System.Linq;
using QuestPlugin.Runtime.Core;
using QuestPlugin.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace QuestPlugin.Editor.QuestGraph
{
    /// <summary>
    /// Custom Unity EditorWindow that renders a node-based quest graph editor.
    /// Supports panning, zooming, drag-to-connect nodes, an inspector panel,
    /// undo/redo, and grid snapping.
    /// </summary>
    public partial class QuestGraphWindow : EditorWindow
    {
        /// <summary>
        /// Stores a screen-space hit rectangle for a rendered connection curve,
        /// used to detect mouse clicks on edges for selection.
        /// </summary>
        private struct EdgeHit
        {
            public Rect Rect;   // Axis-aligned bounding box used for click detection
            public int Index;   // Index into _graph.Connections for fast lookup
        }

        // Hit areas rebuilt each repaint so they stay in sync with the current layout
        private readonly List<EdgeHit> _edgeHits = new();

        // The ScriptableObject that holds all node and connection data for the open graph
        private QuestGraphData _graph;

        // GUI.Window requires integer IDs; these two dictionaries keep the
        // node GUID ↔ window ID mapping consistent across repaints
        private readonly Dictionary<string, int> _guidToWinId = new();
        private readonly Dictionary<int, string> _winIdToGuid = new();
        private int _nextWinId = 1;

        // Node dimensions in unscaled (world-space) units; scaled by _zoom at draw time
        private const float NodeWidth = 240f;
        private const float NodeHeight = 100f;
        private const float HeaderHeight = 28f;  // Reserved for the toolbar strip at the top

        // Null when no node is selected; set to the GUI window ID of the focused node
        private int? _selectedNodeId;

        // Tracks the source node when the user starts dragging a new connection wire
        private int? _pendingFromId;
        private bool _isDraggingConnection;
        private Vector2 _connectionDragMousePos;

        private const float PortSize = 16f;          // Diameter of input/output port circles
        private const float PortOutlineWidth = 2f;   // Stroke width for port rings

        // Canvas navigation state — pan is in screen pixels, zoom is a linear scale factor
        private Vector2 _panOffset = Vector2.zero;
        private float _zoom = 1f;
        private const float ZoomMin = 0.4f;
        private const float ZoomMax = 1.6f;

        private bool _isPanning;
        private int _pendingFromPortIndex;  // Output port index on the node being connected from

        // The inspector panel is docked to the right edge of the window
        private const float InspectorWidth = 460f;

        // Cached SerializedObject for the open graph asset, used to draw
        // polymorphic [SerializeReference] fields via Unity's property drawers
        private SerializedObject _graphSo;
        private string _lastInspectorNodeGuid;  // Detects node switches so the SO cache can be invalidated

        [MenuItem("Tools/Quest Graph")]
        public static void ShowWindow()
        {
            var window = GetWindow<QuestGraphWindow>();
            window.titleContent = new GUIContent("Quest Graph");
            window.minSize = new Vector2(750, 500);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();

            var e = Event.current;

            bool showInspector = _graph != null && _selectedNodeId.HasValue;

            // Full usable area below the header bar
            var full = new Rect(0, HeaderHeight, position.width, position.height - HeaderHeight);

            // Divide horizontally: graph canvas on the left, inspector on the right
            Rect canvasRect = showInspector
                ? new Rect(full.x, full.y, full.width - InspectorWidth, full.height)
                : full;

            Rect inspectorRect = showInspector
                ? new Rect(full.x + full.width - InspectorWidth, full.y, InspectorWidth, full.height)
                : default;

            GUI.Box(canvasRect, GUIContent.none);

            // Clip all canvas drawing to the canvas rect so it never bleeds into the inspector
            GUI.BeginGroup(canvasRect);
            try
            {
                HandleZoom(e);
                HandleLeftMouse(e);
                HandleRightClick(e);
                HandleDeleteKey(e);

                DrawGrid(canvasRect.size);
                DrawConnections();

                // BeginWindows / EndWindows bracket is required for GUI.Window calls
                BeginWindows();
                DrawNodeWindows();
                EndWindows();

                // Ports, selection highlight, and the in-progress connection wire are drawn
                // on top of the node windows so they're never obscured
                DrawPorts();
                DrawSelectedNodeOutline();
                DrawConnectionPreview();
            }
            finally
            {
                GUI.EndGroup();
            }

            if (showInspector)
            {
                GUI.Box(inspectorRect, GUIContent.none);
                DrawInspector(inspectorRect);
            }

            // Keep the editor live during drag operations so the preview wire updates every frame
            if (e.type == EventType.MouseDrag || e.type == EventType.MouseMove)
                Repaint();
        }

        /// <summary>
        /// Renders each node as a draggable GUI.Window and writes back any
        /// position changes (with optional grid snapping) to the graph asset.
        /// </summary>
        private void DrawNodeWindows()
        {
            if (_graph == null) return;

            foreach (var node in _graph.Nodes)
            {
                var winId = GetWindowId(node.Id);

                // Convert the stored world-space position to screen space before drawing
                var screenPos = WorldToScreen(node.Position);
                var rect = new Rect(screenPos.x, screenPos.y, NodeWidth * _zoom, NodeHeight * _zoom);

                var newRect = GUI.Window(winId, rect, DrawNodeWindow, node.Title);

                // Draw a coloured outline outside the window border to visually encode node type
                var typeCol = GetTypeOutlineColor(node.Type);
                var outlineRect = new Rect(
                    newRect.x - (2f * _zoom), newRect.y - (2f * _zoom),
                    newRect.width + (4f * _zoom), newRect.height + (4f * _zoom));
                DrawNodeOutline(outlineRect, typeCol, 2.5f * _zoom);

                // GUI.Window returns the same rect when the user hasn't moved it
                if (newRect.position == rect.position) continue;

                // Persist the new position; hold Shift to bypass grid snapping
                Undo.RecordObject(_graph, "Move Quest Node");
                var worldPos = ScreenToWorld(newRect.position);
                node.Position = Event.current.shift ? worldPos : SnapToGrid(worldPos);
                EditorUtility.SetDirty(_graph);
            }
        }

        /// <summary>
        /// Draws the right-hand inspector panel for the currently selected node.
        /// Renders standard fields directly, and delegates polymorphic condition /
        /// action lists to <see cref="QuestSerializeReferenceUI"/> so Unity's
        /// property drawers handle concrete subtype selection automatically.
        /// </summary>
        private void DrawInspector(Rect r)
        {
            if (_graph == null) return;
            if (!_selectedNodeId.HasValue) return;

            GUILayout.BeginArea(new Rect(r.x + 8, r.y + 8, r.width - 16, r.height - 16));
            try
            {
                var guid = GetGuid(_selectedNodeId.Value);
                if (string.IsNullOrEmpty(guid)) return;

                // Invalidate the cached SerializedObject whenever the selected node changes
                if (_lastInspectorNodeGuid != guid)
                {
                    _lastInspectorNodeGuid = guid;
                    _graphSo = null;
                    GUI.FocusControl(null);  // Clear text field focus to avoid stale input state
                }

                var node = _graph.FindNode(guid);
                if (node == null) return;

                // Lazily create output port entries so newly-added nodes are render-ready
                node.EnsureOutputs();

                EnsureSerializedGraph();
                _graphSo.Update();

                var nodeProp = FindNodePropertyByGuid(node.Id);

                EditorGUI.BeginChangeCheck();

                GUILayout.Label("Node");

                // Read-only metadata — shown for context but not editable by the designer
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Id", node.Id);
                EditorGUILayout.TextField("Type", DisplayNodeType(node.Type));
                EditorGUI.EndDisabledGroup();

                node.Title       = EditorGUILayout.TextField("Title",        node.Title);
                node.Description = EditorGUILayout.TextField("Description",  node.Description);
                node.JsonPayload = EditorGUILayout.TextField("Json Payload", node.JsonPayload);

                GUILayout.Space(8);

                // Condition and Objective nodes share the same condition picker UI;
                // Condition nodes always expose exactly two outputs (true / false branch)
                if (node.Type == QuestNodeType.Condition || node.Type == QuestNodeType.Objective)
                {
                    string label = node.Type == QuestNodeType.Objective ? "Complete When" : "Condition";

                    if (nodeProp != null)
                    {
                        var condProp = nodeProp.FindPropertyRelative("condition");
                        QuestSerializeReferenceUI.DrawCondition(label, condProp);
                    }

                    if (node.Type == QuestNodeType.Condition)
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.IntField("Outputs", 2);  // Fixed; changing it would break branch logic
                        EditorGUI.EndDisabledGroup();
                    }
                }

                // Action nodes support an ordered list of effects that fire on traversal
                if (node.Type == QuestNodeType.Action)
                {
                    GUILayout.Space(8);

                    if (nodeProp != null)
                    {
                        var actionsProp = nodeProp.FindPropertyRelative("actions");
                        QuestSerializeReferenceUI.DrawActionList("Actions", actionsProp);
                    }
                }

                // Sequence nodes have a designer-controlled number of ordered outputs
                if (node.Type == QuestNodeType.Sequence)
                {
                    GUILayout.Space(8);

                    int newCount = EditorGUILayout.IntField("Outputs", node.OutputCount);
                    newCount = Mathf.Clamp(newCount, 1, 16);

                    if (newCount != node.OutputCount)
                    {
                        node.OutputCount = newCount;
                        node.EnsureOutputs();
                        // Remove any connections that referenced ports beyond the new count
                        CleanupConnectionsForNodeOutputs(node.Id, node.OutputCount);
                    }

                    GUILayout.Space(6);
                    GUILayout.Label("Output Labels");

                    for (int i = 0; i < node.OutputCount; i++)
                    {
                        string current = node.GetOutputLabel(i);
                        string next = EditorGUILayout.TextField($"Out {i}", current);
                        if (next != current) node.SetOutputLabel(i, next);
                    }
                }

                GUILayout.Space(10);
                GUILayout.Label("Connections");

                string nodeId = node.Id;

                // Each node accepts at most one incoming connection (single-parent DAG)
                var incoming = GetIncomingTo(nodeId);
                if (incoming == null)
                    GUILayout.Label("Incoming: none");
                else
                {
                    string fromName = NodeTitleOrId(incoming.From.NodeId);
                    GUILayout.Label($"Incoming: {fromName}:{incoming.From.PortIndex} -> This");
                }

                GUILayout.Space(6);
                GUILayout.Label("Outgoing");

                if (node.Type == QuestNodeType.End)
                {
                    GUILayout.Label("Outgoing: none");
                }
                else
                {
                    for (int p = 0; p < node.OutputCount; p++)
                    {
                        var outC = GetOutgoingFrom(nodeId, p);
                        string portLabel = PortLabelForNode(node, p);

                        if (outC == null)
                        {
                            GUILayout.Label($"{portLabel}: none");
                            continue;
                        }

                        string toName = NodeTitleOrId(outC.To.NodeId);
                        GUILayout.Label($"{portLabel}: {toName}");
                    }
                }

                bool changed = EditorGUI.EndChangeCheck();

                _graphSo.ApplyModifiedProperties();

                if (changed)
                {
                    Undo.RecordObject(_graph, "Edit Node");
                    EditorUtility.SetDirty(_graph);
                    Repaint();
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        /// <summary>
        /// Lazily creates or refreshes the <see cref="SerializedObject"/> wrapper
        /// around the current graph asset. Must be called before accessing any
        /// SerializedProperty to ensure the wrapper targets the correct object.
        /// </summary>
        private void EnsureSerializedGraph()
        {
            if (_graph == null)
            {
                _graphSo = null;
                return;
            }

            if (_graphSo == null || _graphSo.targetObject != _graph)
                _graphSo = new SerializedObject(_graph);
        }

        /// <summary>
        /// Walks the serialized <c>nodes</c> array to find the element whose
        /// <c>id</c> field matches <paramref name="nodeGuid"/>. Returning the
        /// <see cref="SerializedProperty"/> lets callers drive Unity's standard
        /// property drawers for nested [SerializeReference] fields.
        /// </summary>
        private SerializedProperty FindNodePropertyByGuid(string nodeGuid)
        {
            if (_graphSo == null) return null;

            var nodesProp = _graphSo.FindProperty("nodes");
            if (nodesProp == null || !nodesProp.isArray) return null;

            for (int i = 0; i < nodesProp.arraySize; i++)
            {
                var nodeProp = nodesProp.GetArrayElementAtIndex(i);
                var idProp   = nodeProp.FindPropertyRelative("id");
                if (idProp != null && idProp.stringValue == nodeGuid)
                    return nodeProp;
            }

            return null;
        }

        /// <summary>
        /// Removes connections from output ports whose index is >= <paramref name="maxOutputs"/>.
        /// Called whenever a Sequence node's output count is reduced so the graph
        /// data doesn't hold dangling references to non-existent ports.
        /// </summary>
        private void CleanupConnectionsForNodeOutputs(string nodeId, int maxOutputs)
        {
            if (_graph == null) return;

            // Iterate backwards so removal doesn't invalidate the current index
            for (int i = _graph.Connections.Count - 1; i >= 0; i--)
            {
                var c = _graph.Connections[i];
                if (c.From.NodeId == nodeId && c.From.PortIndex >= maxOutputs)
                    _graph.Connections.RemoveAt(i);
            }
        }

        /// <summary>
        /// Returns the first connection whose destination is <paramref name="toNodeId"/>.
        /// The graph enforces a single-incoming-edge constraint, so FirstOrDefault is sufficient.
        /// </summary>
        private QuestConnectionData GetIncomingTo(string toNodeId)
        {
            return _graph == null
                ? null
                : _graph.Connections.FirstOrDefault(c => c.To.NodeId == toNodeId);
        }

        /// <summary>
        /// Returns the connection leaving a specific output port, or null if the port is unconnected.
        /// </summary>
        private QuestConnectionData GetOutgoingFrom(string fromNodeId, int fromPort)
        {
            return _graph == null
                ? null
                : _graph.Connections.FirstOrDefault(c =>
                    c.From.NodeId == fromNodeId && c.From.PortIndex == fromPort);
        }

        /// <summary>
        /// Returns the node's title for display in the inspector.
        /// Falls back to the raw GUID if no title has been set, ensuring the UI
        /// always shows something identifiable even for unnamed nodes.
        /// </summary>
        private string NodeTitleOrId(string nodeId)
        {
            if (_graph == null) return nodeId;
            var n = _graph.FindNode(nodeId);
            if (n == null) return nodeId;
            return string.IsNullOrEmpty(n.Title) ? nodeId : n.Title;
        }

        /// <summary>
        /// Resolves a human-readable label for an output port.
        /// Sequence nodes use "Then N" to reflect their ordered-step semantics;
        /// all other node types use the designer-supplied output label.
        /// </summary>
        private string PortLabelForNode(QuestNodeData node, int portIndex)
        {
            if (node == null) return $"Out {portIndex}";
            node.EnsureOutputs();

            if (node.Type == QuestNodeType.Sequence)
                return $"Then {portIndex}";

            return node.GetOutputLabel(portIndex);
        }

        /// <summary>
        /// Maps the internal <see cref="QuestNodeType.Reward"/> enum value to the
        /// designer-facing label "Action" to keep the UI terminology consistent
        /// with how the type is described in design documents.
        /// </summary>
        private static string DisplayNodeType(QuestNodeType t)
        {
            if (t == QuestNodeType.Reward) return "Action";
            return t.ToString();
        }

        /// <summary>
        /// Returns the colour used to draw the outline border around a node,
        /// giving designers an instant visual cue about its role in the graph.
        /// </summary>
        private static Color GetTypeOutlineColor(QuestNodeType type)
        {
            switch (type)
            {
                case QuestNodeType.Start:     return new Color(0.2f,  0.95f, 0.35f, 1f); // green  — entry point
                case QuestNodeType.Objective: return new Color(0.75f, 0.35f, 1f,    1f); // purple — player goal
                case QuestNodeType.Condition: return new Color(1f,    0.25f, 0.25f, 1f); // red    — branch
                case QuestNodeType.Action:    return new Color(0.25f, 0.55f, 1f,    1f); // blue   — effect
                case QuestNodeType.Sequence:  return new Color(1f,    0.85f, 0.25f, 1f); // yellow — ordered steps
                case QuestNodeType.End:       return new Color(0.65f, 0.65f, 0.65f, 1f); // gray   — terminal
                default:                      return new Color(1f,    1f,    1f,    0.35f);
            }
        }

        /// <summary>
        /// Draws an anti-aliased rectangular outline using Handles, independent of
        /// Unity's GUI matrix, so the border is not clipped by the node window itself.
        /// </summary>
        private static void DrawNodeOutline(Rect rect, Color color, float thickness)
        {
            Handles.BeginGUI();
            Handles.color = color;

            // Define corners in clockwise order and close the loop by repeating p1
            var p1 = new Vector3(rect.xMin, rect.yMin, 0f);
            var p2 = new Vector3(rect.xMax, rect.yMin, 0f);
            var p3 = new Vector3(rect.xMax, rect.yMax, 0f);
            var p4 = new Vector3(rect.xMin, rect.yMax, 0f);

            Handles.DrawAAPolyLine(thickness, p1, p2, p3, p4, p1);
            Handles.EndGUI();
        }
    }
}