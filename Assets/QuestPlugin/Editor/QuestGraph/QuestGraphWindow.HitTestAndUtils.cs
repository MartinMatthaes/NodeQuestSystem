using System.Collections.Generic;
using System.Linq;
using QuestPlugin.Runtime.Core;
using QuestPlugin.Runtime.Data;
using UnityEngine;

namespace QuestPlugin.Editor.QuestGraph
{
    public partial class QuestGraphWindow
    {
        // World space is the coordinate system stored in the asset (pan/zoom independent).
        // Screen space is the pixel position used by GUI layout, accounting for pan and zoom.
        private Vector2 WorldToScreen(Vector2 world) => world * _zoom + _panOffset;
        private Vector2 ScreenToWorld(Vector2 screen) => (screen - _panOffset) / _zoom;

        /// <summary>
        /// Returns the hit rectangle for a node's single input port, anchored to the
        /// left edge of the node. The vertical position aligns with the first output
        /// port so that connecting wires appear visually balanced.
        /// </summary>
        private Rect GetInPortRect(Rect nodeRect, QuestNodeData node)
        {
            var size = PortSize * _zoom;
            var yCenter = nodeRect.center.y;

            // Align the input port vertically with the node's first output port,
            // giving incoming wires a natural entry point rather than snapping to centre
            if (node != null && node.Type != QuestNodeType.Start)
            {
                node.EnsureOutputs();

                int outCount = node.OutputCount;
                if (node.Type == QuestNodeType.Condition) outCount = 2;  // Condition always has true/false

                yCenter = GetOutPortCenterY(nodeRect, 0, Mathf.Max(1, outCount));
            }

            var y = yCenter - (size * 0.5f);
            var x = nodeRect.xMin - (size * 0.5f) + (2f * _zoom);
            return new Rect(x, y, size, size);
        }

        /// <summary>
        /// Returns the hit rectangle for one output port on the right edge of a node.
        /// Ports are evenly distributed across the node's usable vertical space,
        /// with padding at the top and bottom so they don't crowd the node borders.
        /// </summary>
        private Rect GetOutPortRect(Rect nodeRect, int outIndex, int outCount)
        {
            var size = PortSize * _zoom;

            var topPad    = 18f * _zoom;
            var bottomPad = 18f * _zoom;

            var usableH = Mathf.Max(1f, nodeRect.height - topPad - bottomPad);

            // When there is only one output, step = 0 so the port stays vertically centred
            var step = outCount <= 1 ? 0f : usableH / (outCount - 1);

            var y = nodeRect.yMin + topPad + step * outIndex - (size * 0.5f);
            var x = nodeRect.xMax - (size * 0.5f) - (2f * _zoom);

            return new Rect(x, y, size, size);
        }

        // Convenience wrappers — connection drawing only needs the centre point,
        // not the full rect, so these avoid repeating .center at every call site
        private Vector2 GetOutPortCenter(Rect nodeRect, int outIndex, int outCount)
            => GetOutPortRect(nodeRect, outIndex, outCount).center;

        private Vector2 GetInPortCenter(Rect nodeRect, QuestNodeData node)
            => GetInPortRect(nodeRect, node).center;

        /// <summary>
        /// Returns the GUI window ID of the topmost node under <paramref name="mousePos"/>,
        /// or -1 if nothing is hit. Clicks that land on an input or output port return -1
        /// so port interactions are handled separately and don't accidentally trigger node dragging.
        /// </summary>
        private int GetNodeIdAtPosition(Vector2 mousePos)
        {
            if (_graph == null) return -1;

            // Iterate in reverse so nodes drawn on top (higher index) are tested first
            for (var i = _graph.Nodes.Count - 1; i >= 0; i--)
            {
                var n = _graph.Nodes[i];

                var screenPos = WorldToScreen(n.Position);
                var screenRect = new Rect(screenPos.x, screenPos.y, NodeWidth * _zoom, NodeHeight * _zoom);

                if (!screenRect.Contains(mousePos)) continue;

                // Port rects extend slightly outside the node window, so check them
                // explicitly before claiming the click belongs to the node body
                if (GetInPortRect(screenRect, n).Contains(mousePos)) continue;

                if (n.Type == QuestNodeType.End) return GetWindowId(n.Id);
                n.EnsureOutputs();
                for (var p = 0; p < n.OutputCount; p++)
                    if (GetOutPortRect(screenRect, p, n.OutputCount).Contains(mousePos))
                        return -1;

                return GetWindowId(n.Id);
            }

            return -1;
        }

        /// <summary>
        /// Returns the GUI window ID of the node whose input port contains
        /// <paramref name="mousePos"/>, used to resolve the drop target when
        /// the user releases a connection drag. Returns -1 if no port is hit.
        /// </summary>
        private int GetInPortNodeAt(Vector2 mousePos)
        {
            if (_graph == null) return -1;

            for (var i = _graph.Nodes.Count - 1; i >= 0; i--)
            {
                var n = _graph.Nodes[i];
                if (n.Type == QuestNodeType.Start) continue;  // Start has no input port

                var screenPos = WorldToScreen(n.Position);
                var screenRect = new Rect(screenPos.x, screenPos.y, NodeWidth * _zoom, NodeHeight * _zoom);

                if (GetInPortRect(screenRect, n).Contains(mousePos))
                    return GetWindowId(n.Id);
            }

            return -1;
        }

        /// <summary>
        /// Returns true if a connection already exists between the two specified ports.
        /// Used as a guard before adding new connections to prevent duplicate edges.
        /// </summary>
        private bool ConnectionExists(string fromId, int fromPort, string toId, int toPort)
        {
            if (_graph == null) return false;

            return _graph.Connections.Any(c =>
                c.From.NodeId    == fromId   &&
                c.From.PortIndex == fromPort &&
                c.To.NodeId      == toId     &&
                c.To.PortIndex   == toPort
            );
        }

        /// <summary>
        /// Returns the GUI.Window integer ID for a node GUID, creating a new mapping
        /// if one doesn't exist yet. GUIDs are stable across sessions (stored in the
        /// asset), while window IDs are transient and rebuilt each time the window opens.
        /// </summary>
        private int GetWindowId(string guid)
        {
            if (_guidToWinId.TryGetValue(guid, out int id))
                return id;

            id = _nextWinId++;
            _guidToWinId[guid] = id;
            _winIdToGuid[id] = guid;
            return id;
        }

        // Reverse lookup — translates a GUI.Window ID back to the persistent node GUID
        private string GetGuid(int windowId)
        {
            return _winIdToGuid.GetValueOrDefault(windowId);
        }

        /// <summary>
        /// Opens the Quest Graph editor for the given asset, reusing an existing
        /// window instance if one is already open rather than spawning a duplicate.
        /// </summary>
        public static QuestGraphWindow Open(QuestGraphData graph)
        {
            var window = GetExistingOrCreate();
            window.titleContent = new GUIContent("Quest Graph");
            window.minSize = new Vector2(750, 500);
            window.Show();
            window.Focus();
            window.SetGraph(graph);
            return window;
        }

        /// <summary>
        /// Returns the first existing <see cref="QuestGraphWindow"/> instance found in
        /// memory, or creates a fresh one if none exists. Using FindObjectsOfTypeAll
        /// catches windows that are open but not currently focused.
        /// </summary>
        private static QuestGraphWindow GetExistingOrCreate()
        {
            var existing = Resources.FindObjectsOfTypeAll<QuestGraphWindow>();
            return existing is { Length: > 0 } ? existing[0] : CreateInstance<QuestGraphWindow>();
        }

        /// <summary>
        /// Replaces the active graph asset and resets all editor state — selection,
        /// drag operations, and the GUID→WindowID cache — so stale references from
        /// the previous graph don't leak into the new one.
        /// </summary>
        private void SetGraph(QuestGraphData g)
        {
            _graph = g;

            _guidToWinId.Clear();
            _winIdToGuid.Clear();
            _nextWinId = 1;

            _selectedNodeId = null;
            CancelConnectionDrag();

            if (_graph != null)
            {
                // Pre-populate the ID cache so every node has a stable window ID
                // assigned before the first OnGUI call renders them
                foreach (QuestNodeData node in _graph.Nodes)
                    GetWindowId(node.Id);
            }

            Repaint();
        }

        /// <summary>
        /// Hit-tests all output ports against <paramref name="mousePos"/> and writes
        /// the result into the out parameters. Used on mouse-down to determine whether
        /// a connection drag should begin and from which port.
        /// </summary>
        /// <returns>True if an output port was hit; false otherwise.</returns>
        private bool TryGetOutPortAt(Vector2 mousePos, out int nodeWinId, out int portIndex)
        {
            nodeWinId  = -1;
            portIndex  = -1;

            if (_graph == null) return false;

            for (var i = _graph.Nodes.Count - 1; i >= 0; i--)
            {
                var n = _graph.Nodes[i];
                if (n.Type == QuestNodeType.End) continue;  // End nodes have no outputs to drag from

                n.EnsureOutputs();
                var outCount = n.OutputCount;

                var screenPos = WorldToScreen(n.Position);
                var nodeRect = new Rect(screenPos.x, screenPos.y, NodeWidth * _zoom, NodeHeight * _zoom);

                for (var p = 0; p < outCount; p++)
                {
                    if (!GetOutPortRect(nodeRect, p, outCount).Contains(mousePos)) continue;
                    nodeWinId = GetWindowId(n.Id);
                    portIndex = p;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the vertical centre of an output port in screen space.
        /// Extracted from <see cref="GetOutPortRect"/> so the input port can
        /// reuse the same positioning logic without constructing a full Rect.
        /// </summary>
        private float GetOutPortCenterY(Rect nodeRect, int outIndex, int outCount)
        {
            var topPad    = 18f * _zoom;
            var bottomPad = 18f * _zoom;

            var usableH = Mathf.Max(1f, nodeRect.height - topPad - bottomPad);
            var step     = outCount <= 1 ? 0f : usableH / (outCount - 1);

            return nodeRect.yMin + topPad + step * outIndex;
        }
    }
}