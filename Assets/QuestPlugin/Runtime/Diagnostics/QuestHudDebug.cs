using System.Text;
using QuestPlugin.Runtime.Core;
using UnityEngine;

namespace QuestPlugin.Runtime.Diagnostics
{
    /// <summary>
    /// Development-only IMGUI overlay that displays live quest state for every quest
    /// managed by the assigned QuestRunner. Attach to any scene GameObject to get an
    /// on-screen read-out of current nodes, variables, and completion status without
    /// needing the Unity Inspector open.
    /// </summary>
    public class QuestHudDebug : MonoBehaviour
    {
        [SerializeField] private QuestRunner runner;

        private Vector2  _scroll;
        private string[] _boxContents;  // Built once per frame in RebuildCache; shared by OnGUI and CalculateContentHeight to guarantee matching heights

        private const float HudWidth   = 500f;
        private const float HudPadding = 10f;
        private const float BoxSpacing = 10f;
        private const float LineHeight = 18f;
        private const float BoxPadding = 24f;  // Vertical padding added on top of line-count height to give each box breathing room

        private void OnGUI()
        {
            if (runner == null || runner.QuestCount == 0) return;

            RebuildCache();

            const float boxWidth = HudWidth - 30f;
            var viewRect    = new Rect(HudPadding, HudPadding, HudWidth, Screen.height - HudPadding * 2f);
            var contentRect = new Rect(0, 0, boxWidth, CalculateContentHeight());

            _scroll = GUI.BeginScrollView(viewRect, _scroll, contentRect);

            var y = 0f;
            foreach (var t in _boxContents)
            {
                var boxHeight = GetBoxHeight(t);
                GUI.Box(new Rect(0, y, boxWidth, boxHeight), t);
                y += boxHeight + BoxSpacing;
            }

            GUI.EndScrollView();
        }

        /// <summary>
        /// Rebuilds the cached box strings each frame. Reallocates the array only when
        /// the quest count changes, avoiding per-frame allocations during normal play.
        /// </summary>
        private void RebuildCache()
        {
            var count = runner.QuestCount;

            if (_boxContents == null || _boxContents.Length != count)
                _boxContents = new string[count];

            for (var i = 0; i < count; i++)
                _boxContents[i] = BuildQuestString(i);
        }

        /// <summary>
        /// Builds the display string for a single quest: header, current node, finish
        /// state, and all context variables if any are present.
        /// </summary>
        private string BuildQuestString(int i)
        {
            var ctx   = runner.GetContext(i);
            var graph = runner.GetGraphs()[i];
            var sb    = new StringBuilder();

            sb.AppendLine($"Quest [{i}]  |  Graph: {(graph != null ? graph.name : "None")}");
            sb.AppendLine($"Finished: {runner.IsFinishedAt(i)}");

            var currentId = runner.GetCurrentNodeId(i);
            if (!string.IsNullOrEmpty(currentId) && graph != null)
            {
                var node = graph.FindNode(currentId);
                sb.AppendLine($"Node: {(node != null ? node.Title : "Unknown")}  ({currentId})");
            }
            else
            {
                sb.AppendLine("Node: —");
            }

            if (ctx != null && ctx.Variables.Count > 0)
            {
                sb.AppendLine("── Vars ──");
                foreach (var kv in ctx.Variables)
                    sb.AppendLine($"  {kv.Key} = {kv.Value}");
            }

            return sb.ToString();
        }

        // Sums the height of every cached box plus spacing to size the ScrollView content rect correctly.
        private float CalculateContentHeight()
        {
            if (_boxContents == null) return 0f;

            var total = 0f;
            foreach (var content in _boxContents)
                total += GetBoxHeight(content) + BoxSpacing;

            return total + BoxSpacing;
        }

        // Estimates box height from line count. Not pixel-perfect but consistent enough
        // that the content rect and the drawn boxes always agree on total scroll height.
        private static float GetBoxHeight(string text)
        {
            if (string.IsNullOrEmpty(text)) return BoxPadding;
            return BoxPadding + text.Split('\n').Length * LineHeight;
        }
    }
}