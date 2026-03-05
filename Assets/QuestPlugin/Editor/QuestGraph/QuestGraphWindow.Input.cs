using QuestPlugin.Runtime.Core;
using QuestPlugin.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace QuestPlugin.Editor.QuestGraph
{
    public partial class QuestGraphWindow
    {
        /// <summary>
        /// Builds and shows a context menu whose contents depend on what is under
        /// the cursor: a node body gets a Delete option, empty canvas gets node
        /// creation options, and an active connection drag gets a Cancel option.
        /// </summary>
        private void HandleRightClick(Event e)
        {
            if (e.type != EventType.MouseDown || e.button != 1) return;

            var mousePos = e.mousePosition;
            var nodeIdUnderMouse = GetNodeIdAtPosition(mousePos);
            var menu = new GenericMenu();

            if (nodeIdUnderMouse != -1)
            {
                var guid = GetGuid(nodeIdUnderMouse);
                var node = _graph != null ? _graph.FindNode(guid) : null;

                // The Start node is the graph's mandatory entry point and must not be deleted
                if (node is { Type: QuestNodeType.Start })
                    menu.AddDisabledItem(new GUIContent("Delete Node"));
                else
                    menu.AddItem(new GUIContent("Delete Node"), false, () => RemoveNode(nodeIdUnderMouse));
            }
            else
            {
                // No node under cursor — offer the full node creation palette
                menu.AddItem(new GUIContent("Add Node/Objective"), false, () => AddNode(mousePos, "Objective Node"));
                menu.AddItem(new GUIContent("Add Node/Condition"), false, () => AddNode(mousePos, "Condition Node"));
                menu.AddItem(new GUIContent("Add Node/Action"),    false, () => AddNode(mousePos, "Action Node"));
                menu.AddItem(new GUIContent("Add Node/Sequence"),  false, () => AddNode(mousePos, "Sequence Node"));
                menu.AddItem(new GUIContent("Add Node/End"),       false, () => AddNode(mousePos, "End Node"));
            }

            // Surfacing a cancel option during a drag prevents the user from getting
            // stuck with an active wire they can't dismiss without right-clicking
            if (_isDraggingConnection || _pendingFromId.HasValue)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Cancel Connection"), false, CancelConnectionDrag);
            }

            menu.ShowAsContext();
            e.Use();
            Repaint();
        }

        /// <summary>
        /// Central dispatcher for all left-mouse interactions on the canvas.
        /// Priority order on MouseDown: Alt+click edge delete → output port drag →
        /// input port select → node body select → empty space (pan + deselect).
        /// On MouseDrag: pan or update connection wire preview.
        /// On MouseUp: finalise connection to existing node or open create-node menu.
        /// </summary>
        private void HandleLeftMouse(Event e)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                var mousePos = e.mousePosition;

                var hitNode = GetNodeIdAtPosition(mousePos);
                var hitIn   = GetInPortNodeAt(mousePos);

                // Alt+click provides a quick shortcut to delete a connection without
                // right-clicking, matching common node editor conventions
                if (e.alt && TryRemoveEdgeAt(mousePos))
                {
                    e.Use();
                    Repaint();
                    return;
                }

                // Output port click — begin dragging a new connection wire
                if (TryGetOutPortAt(mousePos, out int outWinId, out int outPort))
                {
                    _selectedNodeId           = outWinId;
                    _pendingFromId            = outWinId;
                    _pendingFromPortIndex     = outPort;
                    _isDraggingConnection     = true;
                    _connectionDragMousePos   = mousePos;

                    e.Use();
                    Repaint();
                    return;
                }

                // Input port click — select the target node without starting a drag
                if (hitIn != -1)
                {
                    _selectedNodeId = hitIn;
                    e.Use();
                    Repaint();
                    return;
                }

                // Node body click — select that node
                if (hitNode != -1)
                {
                    _selectedNodeId = hitNode;
                    Repaint();
                    return;
                }

                // Empty canvas click — deselect and begin panning
                _selectedNodeId = null;
                _isPanning = true;
                e.Use();
                Repaint();
                return;
            }

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                if (_isPanning)
                {
                    _panOffset += e.delta;
                    e.Use();
                    Repaint();
                    return;
                }

                if (_isDraggingConnection)
                {
                    // Keep the preview wire tip following the cursor each frame
                    _connectionDragMousePos = e.mousePosition;
                    e.Use();
                    Repaint();
                }
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                _isPanning = false;

                if (!_isDraggingConnection || !_pendingFromId.HasValue) return;
                var mousePos = e.mousePosition;

                if (_graph == null)
                {
                    CancelConnectionDrag();
                    e.Use();
                    Repaint();
                    return;
                }

                var fromGuid = GetGuid(_pendingFromId.Value);
                var    fromPort = _pendingFromPortIndex;

                var inWinId = GetInPortNodeAt(mousePos);

                // Dropped on an existing node's input port — wire them together
                if (inWinId != -1 && inWinId != _pendingFromId.Value)
                {
                    var toGuid = GetGuid(inWinId);

                    if (!string.IsNullOrEmpty(fromGuid) && !string.IsNullOrEmpty(toGuid))
                    {
                        // Guard against duplicate edges on the same port pair
                        if (!ConnectionExists(fromGuid, fromPort, toGuid, 0))
                        {
                            Undo.RecordObject(_graph, "Add Connection");
                            _graph.Connections.Add(
                                new QuestConnectionData(
                                    new QuestPortRef(fromGuid, fromPort),
                                    new QuestPortRef(toGuid, 0)
                                )
                            );
                            EditorUtility.SetDirty(_graph);
                        }
                    }

                    CancelConnectionDrag();
                    e.Use();
                    Repaint();
                    return;
                }

                // Dropped on empty canvas — open the create-node menu so the
                // designer can place a new node and have it connected in one action
                if (!string.IsNullOrEmpty(fromGuid))
                {
                    CancelConnectionDrag();  // Reset drag state before the menu opens
                    e.Use();
                    ShowCreateNodeMenu(mousePos, fromGuid, fromPort);
                    return;
                }

                CancelConnectionDrag();
                e.Use();
                Repaint();
            }
        }

        /// <summary>
        /// Deletes the currently selected node when the Delete key is pressed,
        /// unless the user is actively typing in a text field or the selected
        /// node is the protected Start node.
        /// </summary>
        private void HandleDeleteKey(Event e)
        {
            if (e.type != EventType.KeyDown) return;

            // EditorGUIUtility.editingTextField is true whenever a text control has focus,
            // so we avoid nuking a node while the designer is editing its title or description
            if (IsTypingInTextField()) return;

            if (e.keyCode != KeyCode.Delete) return;
            if (!_selectedNodeId.HasValue) return;

            var guid = GetGuid(_selectedNodeId.Value);
            var node = _graph != null ? _graph.FindNode(guid) : null;
            if (node is { Type: QuestNodeType.Start }) return;

            RemoveNode(_selectedNodeId.Value);
            e.Use();
        }

        /// <summary>
        /// Handles scroll wheel zoom, keeping the point currently under the cursor
        /// fixed in world space so the view doesn't jump when zooming in or out.
        /// </summary>
        private void HandleZoom(Event e)
        {
            if (e.type != EventType.ScrollWheel) return;

            var oldZoom = _zoom;
            var delta   = -e.delta.y * 0.03f;
            _zoom = Mathf.Clamp(_zoom + delta, ZoomMin, ZoomMax);

            // Derive the pan correction from the invariant: the world-space point under
            // the mouse must map to the same screen position before and after zooming.
            // Solving for _panOffset: panOffset = mouse - pivot * newZoom
            var mouse  = e.mousePosition;
            var pivot  = (mouse - _panOffset) / oldZoom;
            _panOffset = mouse - pivot * _zoom;

            e.Use();
            Repaint();
        }

        // Thin wrapper around EditorGUIUtility so callers read as plain English
        private static bool IsTypingInTextField()
        {
            return EditorGUIUtility.editingTextField;
        }
    }
}