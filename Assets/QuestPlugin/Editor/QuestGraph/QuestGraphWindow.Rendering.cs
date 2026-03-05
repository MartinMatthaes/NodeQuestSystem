using QuestPlugin.Runtime.Core;
using QuestPlugin.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace QuestPlugin.Editor.QuestGraph
{
    public partial class QuestGraphWindow
    {
        // Minor and major grid line spacings give designers two levels of visual reference.
        // GridSize matches GridSmall so node snapping aligns exactly with the fine grid.
        private const float GridSmall = 20f;
        private const float GridLarge = 100f;
        private const float GridSize  = 20f;

        /// <summary>
        /// Draws the fixed toolbar strip at the top of the window containing the
        /// editor title and a one-line usage hint for designers unfamiliar with the controls.
        /// </summary>
        private void DrawHeader()
        {
            var headerRect = new Rect(0, 0, position.width, HeaderHeight);
            EditorGUI.DrawRect(headerRect, new Color(0.16f, 0.16f, 0.16f, 1f));

            GUI.Label(new Rect(10, 6, position.width - 20, 20),
                "Quest Graph Editor (Prototype)", EditorStyles.boldLabel);

            const string hint = "Right-click to add/delete nodes. Drag nodes normally. Connect via Out triangle to In circle.";
            GUI.Label(new Rect(280, 6, position.width - 290, 20), hint, EditorStyles.miniLabel);
        }

        /// <summary>
        /// Renders the content inside a node's GUI.Window: its type, a truncated ID
        /// for identification, and an inline condition summary for Condition/Objective nodes.
        /// Must call GUI.DragWindow() at the end to keep node dragging functional.
        /// </summary>
        private void DrawNodeWindow(int windowId)
        {
            if (_graph == null) return;

            var guid = GetGuid(windowId);
            if (string.IsNullOrEmpty(guid)) return;

            var node = _graph.FindNode(guid);
            if (node == null) return;

            // Show only the first 6 characters of the GUID to save space while still
            // providing enough context to distinguish nodes during debugging
            var shortId = node.Id.Length >= 6 ? node.Id.Substring(0, 6) : node.Id;
            GUILayout.Label($"Type: {node.Type} ({shortId})");

            if (node.Type is QuestNodeType.Condition or QuestNodeType.Objective)
            {
                // Preview the assigned condition so designers don't have to open the
                // inspector just to verify what logic a node contains
                var text = node.Condition != null ? node.Condition.GetEditorSummary() : "None";
                var style = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                GUILayout.Label(text, style);
            }

            // Required at the end of every GUI.Window callback to allow the window to be dragged
            GUI.DragWindow();
        }

        /// <summary>
        /// Draws all connections as Bezier curves with a dark outline pass and a lighter
        /// front pass for contrast over the grid. Also rebuilds _edgeHits each frame
        /// so Alt+click deletion hit-testing stays in sync with the current layout.
        /// </summary>
        private void DrawConnections()
        {
            if (_graph == null) return;

            _edgeHits.Clear();

            var mouse = Event.current.mousePosition;

            Handles.BeginGUI();
            GUI.color = Color.white;

            for (var i = 0; i < _graph.Connections.Count; i++)
            {
                var c = _graph.Connections[i];

                var from = _graph.FindNode(c.From.NodeId);
                var to   = _graph.FindNode(c.To.NodeId);
                if (from == null || to == null) continue;

                var fromScreen = WorldToScreen(from.Position);
                var toScreen   = WorldToScreen(to.Position);

                var fromRect = new Rect(fromScreen.x, fromScreen.y, NodeWidth * _zoom, NodeHeight * _zoom);
                var toRect   = new Rect(toScreen.x,   toScreen.y,   NodeWidth * _zoom, NodeHeight * _zoom);

                from.EnsureOutputs();
                var outCount = from.OutputCount;

                // Clamp port index so stale connection data never causes an out-of-range draw
                var port = Mathf.Clamp(c.From.PortIndex, 0, Mathf.Max(0, outCount - 1));
                var start = GetOutPortCenter(fromRect, port, outCount);
                var end   = GetInPortCenter(toRect, to);

                // Tangent length of 70 scaled units gives a smooth S-curve at typical zoom levels
                var startTan = start + Vector2.right * (70f * _zoom);
                var endTan   = end   + Vector2.left  * (70f * _zoom);

                var dist   = HandleUtility.DistancePointBezier(mouse, start, end, startTan, endTan);
                var isHover = dist <= 12f;

                // Hovering widens the line and brightens the colour to confirm the edge is clickable
                var backWidth  = isHover ? 14f : 10f;
                var frontWidth = isHover ?  8f :  5f;

                var backColor  = isHover
                    ? new Color(0.10f, 0.10f, 0.10f, 1.00f)
                    : new Color(0.15f, 0.15f, 0.15f, 0.95f);

                var frontColor = isHover
                    ? new Color(1f, 1f, 0.8f, 1f)
                    : new Color(1f, 1f, 1f, 0.98f);

                // Two-pass draw: dark outline first, coloured line on top for depth
                Handles.DrawBezier(start, end, startTan, endTan, backColor,  null, backWidth);
                Handles.DrawBezier(start, end, startTan, endTan, frontColor, null, frontWidth);

                // Hit rect is the axis-aligned bounding box of the curve with generous padding.
                // Precise per-pixel Bezier testing would be too expensive to run every frame.
                var minX = Mathf.Min(start.x, end.x) - 18f;
                var minY = Mathf.Min(start.y, end.y) - 18f;
                var maxX = Mathf.Max(start.x, end.x) + 18f;
                var maxY = Mathf.Max(start.y, end.y) + 18f;

                _edgeHits.Add(new EdgeHit
                {
                    Rect  = new Rect(minX, minY, maxX - minX, maxY - minY),
                    Index = i
                });
            }

            Handles.EndGUI();
            GUI.color = Color.white;
        }

        /// <summary>
        /// Draws a live Bezier preview wire from the source output port to the cursor
        /// while the user is mid-drag. Rendered in blue to visually distinguish it
        /// from committed connections.
        /// </summary>
        private void DrawConnectionPreview()
        {
            if (!_isDraggingConnection || !_pendingFromId.HasValue) return;
            if (_graph == null) return;

            var fromGuid = GetGuid(_pendingFromId.Value);
            if (string.IsNullOrEmpty(fromGuid)) return;

            var from = _graph.FindNode(fromGuid);
            if (from == null) return;

            var fromScreen = WorldToScreen(from.Position);
            var fromRect = new Rect(fromScreen.x, fromScreen.y, NodeWidth * _zoom, NodeHeight * _zoom);

            Handles.BeginGUI();

            from.EnsureOutputs();
            var port = Mathf.Clamp(_pendingFromPortIndex, 0, Mathf.Max(0, from.OutputCount - 1));

            var start    = GetOutPortCenter(fromRect, port, from.OutputCount);
            var end      = _connectionDragMousePos;
            var startTan = start + Vector2.right * (70f * _zoom);
            var endTan   = end   + Vector2.left  * (70f * _zoom);

            // Same two-pass technique as DrawConnections; blue tint signals work-in-progress
            Handles.DrawBezier(start, end, startTan, endTan, new Color(0f, 0f, 0f, 0.65f),   null, 8f);
            Handles.DrawBezier(start, end, startTan, endTan, new Color(0.35f, 0.85f, 1f, 1f), null, 3.5f);

            Handles.EndGUI();
        }

        /// <summary>
        /// Draws input and output ports on every node using Handles primitives so they
        /// render above the GUI.Window layer. Input ports are green circles on the left edge;
        /// output ports are blue right-pointing triangles on the right edge with a port label.
        /// </summary>
        private void DrawPorts()
        {
            if (_graph == null) return;

            Handles.BeginGUI();

            foreach (var n in _graph.Nodes)
            {
                var screenPos = WorldToScreen(n.Position);
                var nodeRect = new Rect(screenPos.x, screenPos.y, NodeWidth * _zoom, NodeHeight * _zoom);

                var hasInput  = n.Type != QuestNodeType.Start;  // Start has no predecessor
                var hasOutput = n.Type != QuestNodeType.End;    // End has no successor

                var baseRadius = (PortSize * 0.35f) * _zoom;

                if (hasInput)
                {
                    var inCenter = GetInPortRect(nodeRect, n).center;

                    // Filled disc + wire ring gives a clear circular port indicator
                    Handles.color = new Color(0.6f, 1f, 0.6f, 0.3f);
                    Handles.DrawSolidDisc(inCenter, Vector3.forward, baseRadius * 0.3f);

                    Handles.color = new Color(0.6f, 1f, 0.6f);
                    Handles.DrawWireDisc(inCenter, Vector3.forward, baseRadius);
                }

                if (!hasOutput) continue;
                n.EnsureOutputs();

                for (var p = 0; p < n.OutputCount; p++)
                {
                    var outRect    = GetOutPortRect(nodeRect, p, n.OutputCount);
                    var outCenter = outRect.center;

                    Handles.color = new Color(0.4f, 0.8f, 1f);

                    // Right-pointing triangle built from three anti-aliased line segments
                    var tip   = new Vector3(outCenter.x + baseRadius,          outCenter.y,                     0f);
                    var left1 = new Vector3(outCenter.x - baseRadius * 0.7f,   outCenter.y - baseRadius * 0.7f, 0f);
                    var left2 = new Vector3(outCenter.x - baseRadius * 0.7f,   outCenter.y + baseRadius * 0.7f, 0f);

                    var lineW = PortOutlineWidth * _zoom;
                    Handles.DrawAAPolyLine(lineW, tip, left1);
                    Handles.DrawAAPolyLine(lineW, left1, left2);
                    Handles.DrawAAPolyLine(lineW, left2, tip);

                    // Subtle centre fill improves visibility at small zoom levels
                    Handles.color = new Color(0.4f, 0.8f, 1f, 0.25f);
                    Handles.DrawSolidDisc(outCenter, Vector3.forward, baseRadius * 0.18f);

                    // Label sits to the left of the triangle so it doesn't overlap the node edge
                    var style = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = Color.white }
                    };
                    var labelPos = new Vector2(
                        outRect.xMin - (30f * _zoom),
                        outCenter.y  - ( 9f * _zoom));
                    Handles.Label(labelPos, n.GetOutputLabel(p), style);
                }
            }

            Handles.EndGUI();
        }

        /// <summary>
        /// Draws a yellow selection highlight around the currently selected node,
        /// expanded slightly outside the node's own border so the ring is always visible.
        /// </summary>
        private void DrawSelectedNodeOutline()
        {
            if (!_selectedNodeId.HasValue || _graph == null) return;

            var guid = GetGuid(_selectedNodeId.Value);
            if (string.IsNullOrEmpty(guid)) return;

            var n = _graph.FindNode(guid);
            if (n == null) return;

            var screenPos = WorldToScreen(n.Position);

            // Expand by 2px on each side so the ring sits outside the type-colour border
            var r = new Rect(
                screenPos.x - 2,
                screenPos.y - 2,
                NodeWidth  * _zoom + 4,
                NodeHeight * _zoom + 4);

            Handles.BeginGUI();
            Handles.color = Color.yellow;
            Handles.DrawAAPolyLine(2f * _zoom,
                new Vector3(r.xMin, r.yMin),
                new Vector3(r.xMax, r.yMin),
                new Vector3(r.xMax, r.yMax),
                new Vector3(r.xMin, r.yMax),
                new Vector3(r.xMin, r.yMin));  // Repeat first point to close the rectangle
            Handles.EndGUI();
        }

        /// <summary>
        /// Draws a two-density background grid: a fine layer for close-up alignment
        /// and a coarse layer for spatial orientation. Both scroll with the pan offset
        /// using modulo arithmetic so lines tile seamlessly across the full canvas.
        /// </summary>
        private void DrawGrid(Vector2 canvasSize)
        {
            Handles.BeginGUI();

            var small = GridSmall * _zoom;
            var large = GridLarge * _zoom;

            // Mod extracts the fractional pan within one grid cell, keeping the
            // grid anchored to world origin regardless of how far the user has panned
            var oSmall = new Vector2(Mod(_panOffset.x, small), Mod(_panOffset.y, small));
            var oLarge = new Vector2(Mod(_panOffset.x, large), Mod(_panOffset.y, large));

            DrawGridLines(canvasSize, small, oSmall, new Color(1f, 1f, 1f, 0.06f));
            DrawGridLines(canvasSize, large, oLarge, new Color(1f, 1f, 1f, 0.12f));

            Handles.EndGUI();
        }

        // Draws one full set of horizontal and vertical lines across the canvas at the given spacing
        private static void DrawGridLines(Vector2 canvasSize, float spacing, Vector2 offset, Color col)
        {
            Handles.color = col;

            for (var x = offset.x; x < canvasSize.x; x += spacing)
                Handles.DrawLine(new Vector3(x, 0f), new Vector3(x, canvasSize.y));

            for (var y = offset.y; y < canvasSize.y; y += spacing)
                Handles.DrawLine(new Vector3(0f, y), new Vector3(canvasSize.x, y));
        }

        /// <summary>
        /// Modulo that always returns a non-negative result. C#'s built-in % operator
        /// preserves the sign of the dividend, which would produce negative offsets for
        /// negative pan values and shift the grid lines off screen.
        /// </summary>
        private static float Mod(float a, float m)
        {
            if (m <= 0f) return 0f;
            var r = a % m;
            if (r < 0f) r += m;
            return r;
        }

        // Rounds a world-space position to the nearest GridSize intersection
        private static Vector2 SnapToGrid(Vector2 pos)
        {
            return new Vector2(
                Mathf.Round(pos.x / GridSize) * GridSize,
                Mathf.Round(pos.y / GridSize) * GridSize);
        }

        // Returns a one-line summary of a node's assigned condition for node body previews
        private static string ConditionPreview(QuestNodeData node)
        {
            if (node == null) return "";
            return node.Condition == null ? "None" : node.Condition.GetEditorSummary();
        }
    }
}