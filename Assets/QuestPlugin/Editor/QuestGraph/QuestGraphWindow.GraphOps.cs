using QuestPlugin.Runtime.Core;
using QuestPlugin.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace QuestPlugin.Editor.QuestGraph
{
    public partial class QuestGraphWindow
    {
        /// <summary>
        /// Creates a new node at <paramref name="pos"/> (world space), infers its
        /// <see cref="QuestNodeType"/> from keywords in <paramref name="nodeTitle"/>,
        /// registers it with the undo system, and auto-selects it in the inspector.
        /// </summary>
        private void AddNode(Vector2 pos, string nodeTitle)
        {
            if (_graph == null) return;

            // Keyword matching against the title string lets the context menu stay
            // data-driven — new node types only need a title entry, not extra switch arms
            QuestNodeType type = true switch
            {
                _ when nodeTitle.Contains("Start")     => QuestNodeType.Start,
                _ when nodeTitle.Contains("Condition") => QuestNodeType.Condition,
                _ when nodeTitle.Contains("Action")    => QuestNodeType.Action,
                _ when nodeTitle.Contains("End")       => QuestNodeType.End,
                _ when nodeTitle.Contains("Sequence")  => QuestNodeType.Sequence,
                _ => QuestNodeType.Objective  // Default for any unrecognised title
            };

            Undo.RecordObject(_graph, "Add Quest Node");
            var data = new QuestNodeData(type, pos, nodeTitle);
            _graph.Nodes.Add(data);
            EditorUtility.SetDirty(_graph);

            // Auto-select so the inspector opens immediately after placement
            _selectedNodeId = GetWindowId(data.Id);

            Repaint();
        }

        /// <summary>
        /// Deletes a node and every connection that references it, then cleans up
        /// any in-progress drag or selection state that pointed to this node.
        /// All mutations are recorded as a single undo step.
        /// </summary>
        private void RemoveNode(int windowId)
        {
            if (_graph == null) return;

            var guid = GetGuid(windowId);
            if (string.IsNullOrEmpty(guid)) return;

            Undo.RecordObject(_graph, "Delete Quest Node");

            // Iterate backwards so index shifts from RemoveAt don't skip elements
            for (var i = _graph.Nodes.Count - 1; i >= 0; i--)
                if (_graph.Nodes[i].Id == guid)
                    _graph.Nodes.RemoveAt(i);

            for (var i = _graph.Connections.Count - 1; i >= 0; i--)
            {
                var c = _graph.Connections[i];
                if (c.From.NodeId == guid || c.To.NodeId == guid)
                    _graph.Connections.RemoveAt(i);
            }

            EditorUtility.SetDirty(_graph);

            // If the user was mid-drag from this node, abort cleanly
            if (_pendingFromId.HasValue && _pendingFromId.Value == windowId)
                CancelConnectionDrag();

            if (_selectedNodeId.HasValue && _selectedNodeId.Value == windowId)
                _selectedNodeId = null;

            Repaint();
        }

        /// <summary>
        /// Resets all connection-drag state back to idle.
        /// Called when a drag is cancelled (source node deleted, Escape pressed, etc.).
        /// </summary>
        private void CancelConnectionDrag()
        {
            _pendingFromId = null;
            _pendingFromPortIndex = 0;
            _isDraggingConnection = false;
            _connectionDragMousePos = Vector2.zero;
        }

        /// <summary>
        /// Checks whether <paramref name="mousePos"/> falls within any registered edge
        /// hit rect and, if so, removes that connection. Iterates in reverse so that
        /// visually topmost edges (drawn last) are tested first.
        /// </summary>
        /// <returns>True if a connection was removed; false if nothing was hit.</returns>
        private bool TryRemoveEdgeAt(Vector2 mousePos)
        {
            if (_graph == null) return false;

            for (var i = _edgeHits.Count - 1; i >= 0; i--)
            {
                var h = _edgeHits[i];
                if (!h.Rect.Contains(mousePos)) continue;

                // Guard against stale hit data from a previous repaint
                if (h.Index < 0 || h.Index >= _graph.Connections.Count) continue;

                Undo.RecordObject(_graph, "Remove Connection");
                _graph.Connections.RemoveAt(h.Index);
                EditorUtility.SetDirty(_graph);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Displays a context menu listing every node type that can be created
        /// directly from an output port drag. End nodes are excluded because they
        /// have no outputs and cannot act as a connection source.
        /// </summary>
        private void ShowCreateNodeMenu(Vector2 mousePos, string fromGuid, int fromPort)
        {
            var fromNode = _graph.FindNode(fromGuid);
            if (fromNode == null) return;
            if (fromNode.Type == QuestNodeType.End) return;

            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Create/Objective"), false, () =>
                CreateNodeAndConnect(QuestNodeType.Objective, mousePos, "Objective Node", fromGuid, fromPort));

            menu.AddItem(new GUIContent("Create/Condition"), false, () =>
                CreateNodeAndConnect(QuestNodeType.Condition, mousePos, "Condition Node", fromGuid, fromPort));

            menu.AddItem(new GUIContent("Create/Action"), false, () =>
                CreateNodeAndConnect(QuestNodeType.Action, mousePos, "Action Node", fromGuid, fromPort));

            menu.AddItem(new GUIContent("Create/Sequence"), false, () =>
                CreateNodeAndConnect(QuestNodeType.Sequence, mousePos, "Sequence Node", fromGuid, fromPort));

            menu.AddItem(new GUIContent("Create/End"), false, () =>
                CreateNodeAndConnect(QuestNodeType.End, mousePos, "End Node", fromGuid, fromPort));

            menu.ShowAsContext();
        }

        /// <summary>
        /// Atomically creates a node and wires it to an existing output port in a
        /// single undoable step. Using an undo group ensures both the node addition
        /// and the connection are rolled back together with a single Ctrl+Z.
        /// </summary>
        private void CreateNodeAndConnect(QuestNodeType type, Vector2 pos, string nodeTitle, string fromGuid, int fromPort)
        {
            if (_graph == null) return;

            // Collapse the node creation and connection into one undo group so
            // Ctrl+Z undoes both at once rather than requiring two key presses
            var undoGroup = Undo.GetCurrentGroup();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Node + Connect");

            Undo.RecordObject(_graph, "Create Node + Connect");

            var newNode = new QuestNodeData(type, pos, nodeTitle);
            _graph.Nodes.Add(newNode);
            _selectedNodeId = GetWindowId(newNode.Id);

            // Skip adding the connection if one already exists on this port to
            // avoid duplicate edges (e.g. if the method is called twice in quick succession)
            if (!ConnectionExists(fromGuid, fromPort, newNode.Id, 0))
            {
                _graph.Connections.Add(
                    new QuestConnectionData(
                        new QuestPortRef(fromGuid, fromPort),
                        new QuestPortRef(newNode.Id, 0)
                    )
                );
            }

            EditorUtility.SetDirty(_graph);
            Undo.CollapseUndoOperations(undoGroup);
            Repaint();
        }
    }
}