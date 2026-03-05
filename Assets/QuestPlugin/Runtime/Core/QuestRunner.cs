using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using QuestPlugin.Runtime.Data;
using QuestPlugin.Runtime.Events;
using UnityEngine;
using UnityEngine.Serialization;

namespace QuestPlugin.Runtime.Core
{
    /// <summary>
    /// Central MonoBehaviour that drives one or more quest graphs at runtime.
    /// Each frame, the runner steps through active quests within a configurable
    /// budget to prevent any single frame from stalling. Handles initialisation,
    /// per-frame graph traversal, event routing, persistence, and debug display.
    /// </summary>
    public class QuestRunner : MonoBehaviour
    {
        [Header("Quests")]
        [SerializeField] private List<QuestGraphData> graphs = new();
        [SerializeField] private GameObject actor;  // The scene object quests execute on behalf of; defaults to this GameObject

        [Header("Runtime Budget")]
        [SerializeField] private int maxStepsPerFrameTotal = 128;    // Hard cap across all quests per frame
        [SerializeField] private int maxStepsPerQuestPerFrame = 64;  // Per-quest cap to prevent one quest starving others

        [Header("Loop Detection")]
        [SerializeField] private bool logFrameLoopDetection = true;  // Logs a warning when a non-waiting node is visited twice in one frame

        [Header("Persistence")]
        [SerializeField] private bool autoLoadOnStart = true;          // Loads save data in Start before the first Update
        [SerializeField] private bool autoSaveOnQuestFinish = true;    // Saves immediately when any quest reaches an End node
        [SerializeField] private bool autoSaveOnDisable = true;        // Saves on component disable/scene unload as a safety net
        [SerializeField] private string saveFilePrefix = "quest_runner_";
        [SerializeField] private string saveFolder = "QuestSaves";
        [GenerateSaveId]
        [SerializeField] private string saveId = "default";  // Unique identifier for this runner's save file; right-click to generate a UUID

        [Header("Objective Evaluation")]
        [SerializeField] private bool useObjectiveCooldown = true;
        [SerializeField] private float objectiveCooldownSeconds = 0.1f;  // Minimum interval between objective condition evaluations

        [Header("Context Bindings")]
        [SerializeField] private List<TransformBinding> transformBindings = new();  // Scene transforms registered into quest contexts at startup

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] [TextArea(6, 14)] private string debugSummary;  // Read-only inspector field updated each frame

        // All running quest instances, parallel to the graphs list
        private readonly List<QuestRuntime> _quests = new();

        // Tracks subscribed GameEvent handlers so they can be cleanly removed on disable
        private readonly Dictionary<GameEvent, Action<GameEventPayload>> _eventHandlers = new();

        // Shared empty list returned by connection lookups that find no results,
        // avoids allocating a new list for every miss
        private static readonly List<QuestConnectionData> EmptyConnections = new(0);

        public bool IsFinished  => PrimaryQuest == null || PrimaryQuest.isFinished;
        public int  QuestCount  => _quests?.Count ?? 0;
        public bool AllFinished => _quests.Count > 0 && _quests.All(q => q.isFinished);

        private QuestRuntime PrimaryQuest  => _quests.Count > 0 ? _quests[0] : null;
        private string       SavePath      => BuildSavePath();
        private GameObject   EffectiveActor => actor != null ? actor : gameObject;

        /// <summary>
        /// Inspector-configured binding between a ContextTransformKey asset and a
        /// scene Transform. Applied to all quest contexts at startup so action nodes
        /// can look up spawn points and parents by key without direct scene references.
        /// </summary>
        [Serializable]
        private class TransformBinding
        {
            public ContextTransformKey key;
            public Transform value;
        }

        /// <summary>
        /// A GameEvent that has been received but not yet processed by a condition check.
        /// Stored in a per-quest inbox queue so events are never missed between frames.
        /// </summary>
        private struct QueuedGameEvent
        {
            public readonly string Name;
            public GameEventPayload Payload;

            public QueuedGameEvent(string name, GameEventPayload payload)
            {
                Name    = name;
                Payload = payload;
            }
        }

        /// <summary>
        /// All mutable runtime state for a single executing quest graph.
        /// Kept separate from QuestGraphData (the asset) so the asset remains
        /// read-only and multiple runners can execute the same graph independently.
        /// </summary>
        [Serializable]
        private class QuestRuntime
        {
            [FormerlySerializedAs("Graph")]         public QuestGraphData graph;
            [FormerlySerializedAs("CurrentNodeId")] public string currentNodeId;
            [FormerlySerializedAs("LastNodeId")]    public string lastNodeId;
            [FormerlySerializedAs("IsFinished")]    public bool isFinished;

            public QuestContext Ctx;
            public Stack<ReturnFrame>              ReturnStack            = new();
            public Dictionary<string, int>         SequenceIndex          = new();  // Tracks which output port each Sequence node has advanced to
            public HashSet<string>                 EnteredNodes           = new();  // Nodes that have been visited at least once this run
            public Dictionary<string, float>       LastObjectiveEvalTime  = new();  // Used to enforce objective evaluation cooldown
            public readonly Queue<QueuedGameEvent> Inbox                  = new();  // Unprocessed incoming events for this quest

            // Tracks non-waiting nodes visited in the current frame to detect infinite loops
            public HashSet<string> VisitedThisFrame = new();

            // Pre-built lookup from (nodeId, portIndex) → outgoing connections for O(1) traversal
            public Dictionary<(string nodeId, int port), List<QuestConnectionData>> ConnectionCache;

            public string CurrentNodeTitle => graph != null ? graph.FindNode(currentNodeId)?.Title ?? "" : "";
            public string LastNodeTitle    => graph != null ? graph.FindNode(lastNodeId)?.Title    ?? "" : "";

            /// <summary>
            /// Builds a dictionary keyed by (nodeId, portIndex) for fast outgoing connection lookup.
            /// Called once on initialisation and again on restart. Linear in the number of connections.
            /// </summary>
            public void BuildConnectionCache()
            {
                ConnectionCache = new Dictionary<(string, int), List<QuestConnectionData>>();

                if (graph == null || graph.Connections == null) return;

                foreach (QuestConnectionData connection in graph.Connections)
                {
                    if (connection == null) continue;

                    var from = connection.From;
                    var to   = connection.To;

                    if (string.IsNullOrEmpty(from.NodeId) || from.PortIndex < 0) continue;
                    if (string.IsNullOrEmpty(to.NodeId)   || to.PortIndex   < 0) continue;

                    var key = (from.NodeId, from.PortIndex);

                    if (!ConnectionCache.TryGetValue(key, out List<QuestConnectionData> list))
                    {
                        list = new List<QuestConnectionData>(1);
                        ConnectionCache[key] = list;
                    }

                    list.Add(connection);
                }
            }

            /// <summary>
            /// Returns outgoing connections from a specific port, building the cache on first use.
            /// Returns the shared empty list (not null) when no connections exist.
            /// </summary>
            public List<QuestConnectionData> GetCachedConnections(string nodeId, int port)
            {
                if (ConnectionCache == null)
                    BuildConnectionCache();

                if (ConnectionCache == null) return EmptyConnections;

                return ConnectionCache.TryGetValue((nodeId, port), out List<QuestConnectionData> connections) && connections != null
                    ? connections
                    : EmptyConnections;
            }
        }

        // Stores the sequence node to return to after a branch completes,
        // enabling Sequence nodes to act as lightweight coroutine continuations
        private struct ReturnFrame
        {
            public string SequenceNodeId;
        }

        // ─── Unity Lifecycle ────────────────────────────────────────────────────────

        private void Start()
        {
            InitializeQuests();
            SubscribeToGraphEvents();

            if (autoLoadOnStart)
                LoadAll();

            // Bindings applied after load so restored state is not overwritten
            ApplyTransformBindings();
            UpdateDebugInfo();
        }

        private void Update()
        {
            if (_quests.Count == 0) return;

            var remainingBudget = Mathf.Max(1, maxStepsPerFrameTotal);

            foreach (var t in _quests)
            {
                if (remainingBudget <= 0) break;
                if (!ShouldProcessQuest(t)) continue;

                var questBudget = Mathf.Clamp(maxStepsPerQuestPerFrame, 1, remainingBudget);
                remainingBudget -= ProcessQuestSteps(t, questBudget);
            }

            UpdateDebugInfo();
        }

        private void OnDisable()
        {
            // Unsubscribe first to prevent events firing into a partially torn-down runner
            UnsubscribeFromAllEvents();

            if (autoSaveOnDisable)
                SaveAll();
        }

        // ─── Initialisation ─────────────────────────────────────────────────────────

        private void InitializeQuests()
        {
            _quests.Clear();

            if (graphs == null || graphs.Count == 0)
            {
                LogErrorAndDisable("No graphs assigned.");
                return;
            }

            for (int i = 0; i < graphs.Count; i++)
            {
                QuestGraphData graph = graphs[i];
                if (graph == null)
                {
                    Debug.LogWarning($"QuestRunner: Graph at index {i} is null. Skipping.");
                    continue;
                }

                _quests.Add(CreateQuestRuntime(graph));
            }

            if (_quests.Count == 0)
                LogErrorAndDisable("No valid graphs to run.");
        }

        private QuestRuntime CreateQuestRuntime(QuestGraphData graph)
        {
            QuestRuntime runtime = new QuestRuntime { graph = graph };
            runtime.BuildConnectionCache();

            if (!TryInitializeQuest(runtime))
            {
                Debug.LogWarning($"QuestRunner: Failed to init quest graph '{graph.name}'. Marking as finished.");
                runtime.isFinished = true;
            }

            // QuestIndex is set after adding to the list; here we use the count as the
            // prospective index since the runtime hasn't been added to _quests yet
            if (runtime.Ctx != null)
                runtime.Ctx.QuestIndex = _quests.Count;

            return runtime;
        }

        /// <summary>
        /// Returns the node ID that the first outgoing connection on a given port points to,
        /// or null if no connection exists. Always takes port 0 for single-output nodes.
        /// </summary>
        private string GetFirstOutgoingNodeId(QuestRuntime quest, string nodeId, int port)
        {
            if (quest == null) return null;

            List<QuestConnectionData> connections = quest.GetCachedConnections(nodeId, port);
            if (connections == null || connections.Count == 0) return null;

            QuestConnectionData c = connections[0];
            if (c == null) return null;

            // QuestPortRef is a struct so it is never null; check the node ID string instead
            string toNodeId = c.To.NodeId;
            return string.IsNullOrEmpty(toNodeId) ? null : toNodeId;
        }

        private bool TryInitializeQuest(QuestRuntime quest)
        {
            if (quest.graph == null) return false;

            quest.Ctx = new QuestContext(EffectiveActor, this);

            QuestNodeData startNode = FindStartNode(quest.graph);
            if (startNode == null)
            {
                Debug.LogError($"QuestRunner: Graph '{quest.graph.name}' has no Start node.");
                return false;
            }

            startNode.EnsureOutputs();
            if (startNode.OutputCount <= 0)
            {
                Debug.LogWarning($"QuestRunner: Graph '{quest.graph.name}' Start has zero outputs. Quest ends immediately.");
                quest.isFinished = true;
                return true;
            }

            string firstNodeId = GetFirstOutgoingNodeId(quest, startNode.Id, 0);
            if (string.IsNullOrEmpty(firstNodeId))
            {
                Debug.LogWarning($"QuestRunner: Graph '{quest.graph.name}' Start has no outgoing connection on port 0. Quest ends immediately.");
                quest.isFinished = true;
                return true;
            }

            ResetQuestState(quest, firstNodeId);
            return true;
        }

        private static QuestNodeData FindStartNode(QuestGraphData graph)
        {
            if (graph?.Nodes == null) return null;

            foreach (QuestNodeData n in graph.Nodes)
            {
                if (n is { Type: QuestNodeType.Start })
                    return n;
            }

            return null;
        }

        /// <summary>
        /// Resets all mutable quest state to the given start node, clearing every
        /// in-progress tracker. Used both on first initialisation and on restart.
        /// </summary>
        private static void ResetQuestState(QuestRuntime quest, string startNodeId)
        {
            quest.currentNodeId = startNodeId;
            quest.lastNodeId    = null;
            quest.isFinished    = false;
            quest.ReturnStack.Clear();
            quest.SequenceIndex.Clear();
            quest.EnteredNodes.Clear();
            quest.LastObjectiveEvalTime.Clear();
            quest.Inbox.Clear();
            quest.VisitedThisFrame.Clear();
        }

        private void LogErrorAndDisable(string message)
        {
            Debug.LogError($"QuestRunner: {message}");
            enabled = false;
        }

        // ─── Per-Frame Traversal ────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if a quest is in a runnable state: not null, has a graph,
        /// is not finished, and has a valid current node to process.
        /// </summary>
        private static bool ShouldProcessQuest(QuestRuntime quest)
        {
            return quest != null && quest.graph != null && !quest.isFinished && !string.IsNullOrEmpty(quest.currentNodeId);
        }

        /// <summary>
        /// Advances a single quest by up to budget steps. Objective and Condition nodes
        /// are "waiting" nodes — they may return themselves to stall traversal until their
        /// condition is met. All other node types are expected to advance each step;
        /// if a non-waiting node returns itself, the loop guard fires to prevent infinite loops.
        /// </summary>
        private int ProcessQuestSteps(QuestRuntime quest, int budget)
        {
            int stepsTaken = 0;
            quest.VisitedThisFrame.Clear();

            while (budget-- > 0 && ShouldProcessQuest(quest))
            {
                stepsTaken++;

                QuestNodeData currentNode = quest.graph.FindNode(quest.currentNodeId);
                bool isWaitingNode = currentNode is { Type: QuestNodeType.Objective or QuestNodeType.Condition };

                if (!isWaitingNode)
                {
                    // If a non-waiting node has already been visited this frame, the graph
                    // contains a cycle that would loop forever — log and break out
                    if (!quest.VisitedThisFrame.Add(quest.currentNodeId))
                    {
                        if (logFrameLoopDetection)
                        {
                            Debug.LogWarning(
                                $"<b>QuestRunner Loop Guard Triggered</b>\n" +
                                $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                                $"Graph: {quest.graph?.name}\n" +
                                $"Node: {quest.CurrentNodeTitle} ({quest.currentNodeId})\n" +
                                $"Steps This Frame: {stepsTaken}\n" +
                                $"Budget Remaining: {budget}\n" +
                                $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                            );
                        }
                        // break;
                    }
                }

                string nextNode = ExecuteNode(quest, quest.currentNodeId);

                // A non-waiting node that returns itself signals it has nothing to do this frame
                if (!isWaitingNode && nextNode == quest.currentNodeId)
                    break;

                quest.currentNodeId = nextNode;
            }

            return stepsTaken;
        }

        /// <summary>
        /// Dispatches execution to the appropriate handler for the current node type
        /// and returns the ID of the next node to move to, or null to finish the quest.
        /// </summary>
        private string ExecuteNode(QuestRuntime quest, string nodeId)
        {
            quest.lastNodeId = nodeId;

            QuestNodeData node = quest.graph.FindNode(nodeId);
            if (node == null)
            {
                Debug.LogWarning($"QuestRunner: Missing node. Graph '{quest.graph.name}' stopped.");
                FinishQuest(quest);
                return null;
            }

            return node.Type switch
            {
                QuestNodeType.Sequence  => ProcessSequenceNode(quest, node),
                QuestNodeType.Condition => ProcessConditionNode(quest, node),
                QuestNodeType.End       => ProcessEndNode(quest),
                QuestNodeType.Objective => ProcessObjectiveNode(quest, node),
                QuestNodeType.Action    => ProcessActionNode(quest, node),
                _                       => ProcessDefaultNode(quest, node)
            };
        }

        /// <summary>
        /// Advances through the Sequence node's outputs one at a time, pushing a return
        /// frame before each branch so the runner comes back here after each sub-graph completes.
        /// When all outputs have been visited, pops the return stack or finishes the quest.
        /// </summary>
        private string ProcessSequenceNode(QuestRuntime quest, QuestNodeData node)
        {
            node.EnsureOutputs();

            int currentIndex = quest.SequenceIndex.GetValueOrDefault(node.Id, 0);

            while (currentIndex < node.OutputCount)
            {
                string nextId = GetFirstOutgoingNodeId(quest, node.Id, currentIndex);

                currentIndex++;
                quest.SequenceIndex[node.Id] = currentIndex;

                if (!string.IsNullOrEmpty(nextId))
                {
                    // Push a return frame so after the branch finishes we come back to
                    // this Sequence node and continue with the next output port
                    quest.ReturnStack.Push(new ReturnFrame { SequenceNodeId = node.Id });
                    return nextId;
                }
            }

            // All outputs exhausted — remove the tracking entry and return up the stack
            quest.SequenceIndex.Remove(node.Id);
            return PopReturnStackOrFinish(quest);
        }

        /// <summary>
        /// Evaluates the node's condition and routes to port 0 (true) or port 1 (false).
        /// A missing condition is treated as false to fail safely rather than silently advancing.
        /// </summary>
        private string ProcessConditionNode(QuestRuntime quest, QuestNodeData node)
        {
            node.EnsureOutputs();

            bool result = node.Condition != null && node.Condition.Evaluate(quest.Ctx);
            int  port   = result ? 0 : 1;

            string nextId = GetFirstOutgoingNodeId(quest, node.Id, port);
            if (!string.IsNullOrEmpty(nextId))
                return nextId;

            return PopReturnStackOrFinish(quest);
        }

        private string ProcessEndNode(QuestRuntime quest)
        {
            FinishQuest(quest);
            return null;
        }

        /// <summary>
        /// Polls the objective's completion condition, respecting the evaluation cooldown.
        /// Clears the event inbox on first entry so stale events from before this objective
        /// was reached don't immediately satisfy it.
        /// Returns the node's own ID to stall traversal until the condition passes.
        /// </summary>
        private string ProcessObjectiveNode(QuestRuntime quest, QuestNodeData node)
        {
            if (!quest.EnteredNodes.Contains(node.Id))
            {
                // Purge inbox on first entry — events raised before this node was reached
                // are not relevant to this objective's completion check
                ClearInbox(quest);
                quest.EnteredNodes.Add(node.Id);
            }

            if (ShouldThrottleObjectiveEvaluation(quest, node))
                return node.Id;  // Not enough time has passed since the last evaluation

            bool isComplete = node.Condition != null && node.Condition.Evaluate(quest.Ctx);
            if (!isComplete)
                return node.Id;  // Stay on this node until the condition is satisfied

            string nextId = GetFirstOutgoingNodeId(quest, node.Id, 0);
            if (!string.IsNullOrEmpty(nextId))
                return nextId;

            return PopReturnStackOrFinish(quest);
        }

        /// <summary>
        /// Returns true if the objective should be skipped this frame due to the cooldown,
        /// and updates the last evaluation timestamp when it does allow evaluation.
        /// </summary>
        private bool ShouldThrottleObjectiveEvaluation(QuestRuntime quest, QuestNodeData node)
        {
            if (!useObjectiveCooldown || objectiveCooldownSeconds <= 0f)
                return false;

            float now = Time.time;

            if (quest.LastObjectiveEvalTime.TryGetValue(node.Id, out float lastEval))
            {
                if (now - lastEval < objectiveCooldownSeconds)
                    return true;
            }

            quest.LastObjectiveEvalTime[node.Id] = now;
            return false;
        }

        private string ProcessActionNode(QuestRuntime quest, QuestNodeData node)
        {
            ExecuteNodeActions(node, quest.Ctx);

            string nextId = GetFirstOutgoingNodeId(quest, node.Id, 0);
            if (!string.IsNullOrEmpty(nextId))
                return nextId;

            return PopReturnStackOrFinish(quest);
        }

        private string ProcessDefaultNode(QuestRuntime quest, QuestNodeData node)
        {
            quest.EnteredNodes.Add(node.Id);

            string nextId = GetFirstOutgoingNodeId(quest, node.Id, 0);
            if (!string.IsNullOrEmpty(nextId))
                return nextId;

            return PopReturnStackOrFinish(quest);
        }

        /// <summary>
        /// Returns to the most recent Sequence node via the return stack, or finishes
        /// the quest if the stack is empty. This is the mechanism that allows Sequence
        /// nodes to behave like coroutine continuations across multiple branches.
        /// </summary>
        private string PopReturnStackOrFinish(QuestRuntime quest)
        {
            if (quest.ReturnStack.Count > 0)
                return quest.ReturnStack.Pop().SequenceNodeId;

            FinishQuest(quest);
            return null;
        }

        private void ExecuteNodeActions(QuestNodeData node, QuestContext context)
        {
            if (node.Actions == null) return;

            foreach (QuestAction action in node.Actions)
                action?.Execute(context);
        }

        private void FinishQuest(QuestRuntime quest)
        {
            if (quest.isFinished) return;  // Guard against double-finish from re-entrant calls

            quest.isFinished = true;
            Debug.Log($"Quest finished. Graph '{quest.graph?.name ?? "None"}'.");

            if (autoSaveOnQuestFinish)
                SaveAll();
        }

        // ─── Event System ───────────────────────────────────────────────────────────

        /// <summary>
        /// Scans all graph nodes for conditions and actions that implement IUsesGameEvent
        /// and subscribes to each unique event asset found. Called once at startup so
        /// the runner only listens to events that are actually referenced by the graphs.
        /// </summary>
        private void SubscribeToGraphEvents()
        {
            HashSet<GameEvent> eventsToSubscribe = new HashSet<GameEvent>();

            if (graphs == null) return;

            foreach (QuestGraphData graph in graphs)
            {
                if (graph?.Nodes == null) continue;

                foreach (QuestNodeData node in graph.Nodes)
                {
                    if (node == null) continue;
                    AddEventFromCondition(node.Condition, eventsToSubscribe);
                    AddEventsFromActions(node.Actions, eventsToSubscribe);
                }
            }

            foreach (GameEvent evt in eventsToSubscribe)
                SubscribeToEvent(evt);
        }

        // Checks whether a condition implements IUsesGameEvent and adds its asset to the set
        private static void AddEventFromCondition(object condition, HashSet<GameEvent> events)
        {
            if (condition is IUsesGameEvent eventUser)
            {
                GameEvent evt = eventUser.GetEventAsset();
                if (evt != null) events.Add(evt);
            }
        }

        // Checks each action in the list for IUsesGameEvent and collects their assets
        private static void AddEventsFromActions(IEnumerable<QuestAction> actions, HashSet<GameEvent> events)
        {
            if (actions == null) return;

            foreach (QuestAction action in actions)
            {
                if (action is IUsesGameEvent eventUser)
                {
                    GameEvent evt = eventUser.GetEventAsset();
                    if (evt != null) events.Add(evt);
                }
            }
        }

        private void SubscribeToEvent(GameEvent evt)
        {
            if (evt == null) return;
            if (_eventHandlers.ContainsKey(evt)) return;  // Already subscribed; avoid duplicate handlers

            Action<GameEventPayload> handler = payload => OnGameEventRaised(evt, payload);
            _eventHandlers[evt] = handler;
            evt.OnRaised += handler;
        }

        private void UnsubscribeFromAllEvents()
        {
            foreach (KeyValuePair<GameEvent, Action<GameEventPayload>> kvp in _eventHandlers)
            {
                if (kvp.Key != null)
                    kvp.Key.OnRaised -= kvp.Value;
            }
            _eventHandlers.Clear();
        }

        /// <summary>
        /// Enqueues the event into every active quest's inbox. Clearing the objective
        /// cooldown timestamps ensures the event is processed on the very next Update
        /// rather than waiting for the cooldown to expire.
        /// </summary>
        public void OnGameEventRaised(GameEvent evt, GameEventPayload payload)
        {
            if (evt == null || string.IsNullOrEmpty(evt.name)) return;

            foreach (QuestRuntime quest in _quests)
            {
                if (quest == null || quest.isFinished || quest.Ctx == null) continue;

                quest.Inbox.Enqueue(new QueuedGameEvent(evt.name, payload));

                // Reset cooldown so the next Update evaluates objectives immediately
                if (useObjectiveCooldown)
                    quest.LastObjectiveEvalTime.Clear();
            }
        }

        /// <summary>
        /// Scans the quest's inbox for an event matching eventName. If consume is true,
        /// the matching entry is removed so it can only satisfy the condition once.
        /// Non-matching entries are re-queued to preserve them for other condition checks.
        /// </summary>
        public bool HasInboxEvent(int questIndex, string eventName, bool consume)
        {
            if (!IsValidQuestIndex(questIndex) || string.IsNullOrEmpty(eventName))
                return false;

            QuestRuntime quest = _quests[questIndex];
            if (quest == null || quest.Inbox.Count == 0)
                return false;

            bool found     = false;
            int  inboxSize = quest.Inbox.Count;

            // Drain and re-queue the inbox, dropping only the first matching event if consuming
            for (int i = 0; i < inboxSize; i++)
            {
                QueuedGameEvent e = quest.Inbox.Dequeue();

                if (!found && e.Name == eventName)
                {
                    found = true;
                    if (!consume)
                        quest.Inbox.Enqueue(e);  // Not consuming — put it back
                }
                else
                {
                    quest.Inbox.Enqueue(e);  // Unrelated event — preserve it
                }
            }

            return found;
        }

        private static void ClearInbox(QuestRuntime quest)
        {
            quest.Inbox.Clear();
        }

        // ─── Public API ─────────────────────────────────────────────────────────────

        /// <summary>Returns the context for the primary (first) quest, or null if none exists.</summary>
        public QuestContext GetContext() => PrimaryQuest?.Ctx;

        /// <summary>Returns the context for the quest at the given index, or null if out of range.</summary>
        public QuestContext GetContext(int index) => IsValidQuestIndex(index) ? _quests[index].Ctx : null;

        /// <summary>Returns the current node ID for the quest at the given index.</summary>
        public string GetCurrentNodeId(int index) => IsValidQuestIndex(index) ? _quests[index].currentNodeId : null;

        /// <summary>Returns true if the quest at the given index has reached an End node.</summary>
        public bool IsFinishedAt(int index) => IsValidQuestIndex(index) && _quests[index].isFinished;

        /// <summary>Returns the list of graph assets assigned to this runner.</summary>
        public IReadOnlyList<QuestGraphData> GetGraphs() => graphs;

        /// <summary>Immediately marks the primary quest as finished, as if it reached an End node.</summary>
        public void Complete()
        {
            if (PrimaryQuest != null)
            {
                FinishQuest(PrimaryQuest);
                UpdateDebugInfo();
            }
        }

        /// <summary>Immediately marks all quests as finished without normal graph traversal.</summary>
        public void CompleteAll()
        {
            foreach (QuestRuntime t in _quests)
            {
                if (t != null)
                    t.isFinished = true;
            }
            UpdateDebugInfo();
        }

        /// <summary>Restarts all quests from their Start nodes, preserving context variable state.</summary>
        public void RestartAll()
        {
            foreach (QuestRuntime quest in _quests)
            {
                if (quest?.graph == null) continue;
                quest.BuildConnectionCache();
                TryInitializeQuest(quest);
            }
            UpdateDebugInfo();
        }

        /// <summary>Restarts a specific quest graph by asset reference.</summary>
        public void RestartGraph(QuestGraphData graph)
        {
            QuestRuntime quest = FindQuestByGraph(graph);
            if (quest != null)
            {
                quest.BuildConnectionCache();
                TryInitializeQuest(quest);
                UpdateDebugInfo();
            }
        }

        /// <summary>
        /// Sets a variable on all quest contexts simultaneously. Clears objective
        /// cooldown timestamps so the new value is tested on the next Update.
        /// </summary>
        public void SetVariable(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) return;

            foreach (var quest in _quests)
            {
                var ctx = quest?.Ctx;
                if (ctx == null) continue;

                ctx.SetVar(key, value);

                if (useObjectiveCooldown)
                    quest.LastObjectiveEvalTime.Clear();
            }
        }

        /// <summary>Returns true if the named boolean variable is true in any quest context.</summary>
        public bool AnyVariableTrue(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            foreach (QuestRuntime quest in _quests)
            {
                if (quest?.Ctx != null && quest.Ctx.GetVar<bool>(key))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Searches all quest contexts for a variable matching key and type T.
        /// Returns true and sets value on the first match found.
        /// </summary>
        public bool TryGetVariable<T>(string key, out T value)
        {
            value = default;

            if (string.IsNullOrEmpty(key)) return false;

            foreach (QuestRuntime t in _quests)
            {
                QuestContext ctx = t?.Ctx;
                if (ctx == null) continue;

                if (ctx.Variables.TryGetValue(key, out object raw) && raw is T typedValue)
                {
                    value = typedValue;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns the context for a quest by index via an out parameter pattern.</summary>
        public bool TryGetContext(int questIndex, out QuestContext context)
        {
            context = null;
            if (!IsValidQuestIndex(questIndex)) return false;

            context = _quests[questIndex]?.Ctx;
            return context != null;
        }

        /// <summary>Returns the context for a quest whose graph matches the given GraphId string.</summary>
        public bool TryGetContextByGraphId(string graphId, out QuestContext context)
        {
            context = null;
            if (string.IsNullOrEmpty(graphId)) return false;

            foreach (QuestRuntime q in _quests)
            {
                if (q?.graph?.GraphId == graphId)
                {
                    context = q.Ctx;
                    return context != null;
                }
            }

            return false;
        }

        /// <summary>Logs the current value of a variable across all quest contexts. For debugging only.</summary>
        public void DebugVariable(string key)
        {
            for (int i = 0; i < _quests.Count; i++)
            {
                QuestContext context = _quests[i]?.Ctx;
                if (context == null) continue;

                object v = context.Variables.GetValueOrDefault(key);
                Debug.Log($"Quest {i} {key} = {(v != null ? v.ToString() : "null")}");
            }
        }

        // ─── Helpers ────────────────────────────────────────────────────────────────

        private bool IsValidQuestIndex(int index) => index >= 0 && index < _quests.Count;

        private QuestRuntime FindQuestByGraph(QuestGraphData graph)
        {
            if (graph == null) return null;
            foreach (QuestRuntime q in _quests)
                if (q?.graph == graph) return q;
            return null;
        }

        private QuestRuntime FindQuestByGraphId(string graphId)
        {
            if (string.IsNullOrEmpty(graphId)) return null;
            foreach (QuestRuntime q in _quests)
                if (q?.graph?.GraphId == graphId) return q;
            return null;
        }

        // ─── Persistence ────────────────────────────────────────────────────────────

        /// <summary>
        /// Constructs the full file path for the save file, creating the directory if needed.
        /// Uses Application.persistentDataPath as the root so saves are stored in the
        /// platform-appropriate user data location on all target platforms.
        /// </summary>
        private string BuildSavePath()
        {
            string id        = string.IsNullOrEmpty(saveId)         ? "default"       : saveId;
            string prefix    = string.IsNullOrEmpty(saveFilePrefix) ? "quest_runner_" : saveFilePrefix;
            string filename  = prefix + id + ".json";

            string folder    = saveFolder?.Trim() ?? "";
            string root      = Application.persistentDataPath;
            string directory = string.IsNullOrEmpty(folder) ? root : Path.Combine(root, folder);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            return Path.Combine(directory, filename);
        }

        public bool HasSave()   => File.Exists(SavePath);
        public void DeleteSave() { if (File.Exists(SavePath)) File.Delete(SavePath); }

        /// <summary>
        /// Serialises all quest runtimes to a single JSON file. Exceptions are caught
        /// and logged as warnings so a save failure doesn't crash the game mid-session.
        /// </summary>
        public void SaveAll()
        {
            try
            {
                QuestRunnerSaveData saveData = new QuestRunnerSaveData();

                foreach (QuestRuntime quest in _quests)
                {
                    if (quest?.graph == null || quest.Ctx == null) continue;
                    saveData.quests.Add(CreateSaveEntry(quest));
                }

                File.WriteAllText(SavePath, JsonUtility.ToJson(saveData, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"QuestRunner: Save failed. {ex.Message}");
            }
        }

        /// <summary>
        /// Captures all state needed to resume a quest: current node, return stack,
        /// sequence progress, entered nodes, and all context variables.
        /// The return stack is serialised as a flat list and reversed on load to
        /// reconstruct the correct Stack order (last-in-first-out).
        /// </summary>
        private static QuestSaveEntry CreateSaveEntry(QuestRuntime quest)
        {
            QuestSaveEntry entry = new QuestSaveEntry
            {
                graphId       = quest.graph.GraphId,
                graphTitle    = quest.graph.Title,
                currentNodeId = quest.currentNodeId,
                lastNodeId    = quest.lastNodeId,
                isFinished    = quest.isFinished,
                variables     = quest.Ctx.ExportVars()
            };

            entry.returnStackSequenceNodeIds.Clear();
            foreach (ReturnFrame frame in quest.ReturnStack)
                entry.returnStackSequenceNodeIds.Add(frame.SequenceNodeId);

            // SequenceIndex is serialised as parallel key/value lists since
            // JsonUtility does not support Dictionary serialisation directly
            entry.sequenceIndexKeys.Clear();
            entry.sequenceIndexValues.Clear();
            foreach (KeyValuePair<string, int> kvp in quest.SequenceIndex)
            {
                entry.sequenceIndexKeys.Add(kvp.Key);
                entry.sequenceIndexValues.Add(kvp.Value);
            }

            entry.enteredNodeIds.Clear();
            foreach (string nodeId in quest.EnteredNodes)
                entry.enteredNodeIds.Add(nodeId);

            return entry;
        }

        /// <summary>
        /// Deserialises the save file and applies each entry to the matching quest runtime.
        /// Quests are matched by GraphId so reordering graphs in the inspector doesn't
        /// corrupt save data. Returns false if no save file exists or loading fails.
        /// </summary>
        public bool LoadAll()
        {
            try
            {
                if (!File.Exists(SavePath)) return false;

                QuestRunnerSaveData saveData = JsonUtility.FromJson<QuestRunnerSaveData>(File.ReadAllText(SavePath));
                if (saveData?.quests == null) return false;

                foreach (QuestSaveEntry entry in saveData.quests)
                {
                    if (entry == null) continue;

                    QuestRuntime quest = FindQuestByGraphId(entry.graphId);
                    if (quest == null) continue;

                    ApplySaveEntryToQuest(quest, entry);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"QuestRunner: Load failed. {ex.Message}");
                return false;
            }
        }

        private static void ApplySaveEntryToQuest(QuestRuntime quest, QuestSaveEntry entry)
        {
            quest.currentNodeId = entry.currentNodeId;
            quest.lastNodeId    = entry.lastNodeId;
            quest.isFinished    = entry.isFinished;

            // Reverse iteration reconstructs the correct Stack order since the list was
            // serialised from top to bottom but Stack.Push adds to the top
            quest.ReturnStack.Clear();
            if (entry.returnStackSequenceNodeIds != null)
            {
                for (int i = entry.returnStackSequenceNodeIds.Count - 1; i >= 0; i--)
                    quest.ReturnStack.Push(new ReturnFrame { SequenceNodeId = entry.returnStackSequenceNodeIds[i] });
            }

            quest.SequenceIndex.Clear();
            if (entry.sequenceIndexKeys != null && entry.sequenceIndexValues != null)
            {
                int count = Mathf.Min(entry.sequenceIndexKeys.Count, entry.sequenceIndexValues.Count);
                for (int i = 0; i < count; i++)
                    quest.SequenceIndex[entry.sequenceIndexKeys[i]] = entry.sequenceIndexValues[i];
            }

            quest.EnteredNodes.Clear();
            if (entry.enteredNodeIds != null)
            {
                foreach (string nodeId in entry.enteredNodeIds)
                    if (!string.IsNullOrEmpty(nodeId))
                        quest.EnteredNodes.Add(nodeId);
            }

            // Cooldown timestamps and inbox are not persisted — they are transient runtime state
            quest.LastObjectiveEvalTime.Clear();
            quest.Inbox.Clear();

            quest.Ctx.ImportVars(entry.variables);
        }

        /// <summary>
        /// Writes all configured transform bindings into every quest context so action nodes
        /// can look up scene transforms by key. Called after LoadAll so bindings always
        /// reflect the current scene regardless of what was saved.
        /// </summary>
        private void ApplyTransformBindings()
        {
            if (transformBindings == null) return;

            foreach (var binding in transformBindings)
            {
                if (binding?.key == null) continue;

                var id = binding.key.id;
                if (string.IsNullOrEmpty(id)) continue;

                foreach (var quest in _quests)
                    quest?.Ctx?.SetVar(id, binding.value);
            }

            // Reset cooldowns so objectives re-evaluate with the new bindings on the next frame
            if (useObjectiveCooldown)
            {
                foreach (var quest in _quests)
                    quest?.LastObjectiveEvalTime.Clear();
            }
        }

        // ─── Debug ──────────────────────────────────────────────────────────────────

        private void UpdateDebugInfo()
        {
            if (!showDebugInfo) return;

            var sb = new StringBuilder();

            // ── Runner ──────────────────────────────────────────────
            sb.AppendLine($"Actor: {(actor != null ? actor.name : gameObject.name)}");
            sb.AppendLine($"Quests: {_quests.Count}");
            sb.AppendLine($"AllFinished: {AllFinished}");
            sb.AppendLine($"BudgetTotal: {maxStepsPerFrameTotal}");
            sb.AppendLine($"BudgetPerQuest: {maxStepsPerQuestPerFrame}");
            sb.AppendLine();

            for (int i = 0; i < _quests.Count; i++)
            {
                var quest = _quests[i];
                var ctx  = quest?.Ctx;

                // ── Quest header ─────────────────────────────────────
                sb.AppendLine($"[{i}] Graph: {(quest?.graph != null ? quest.graph.name : "None")}");
                sb.AppendLine($"Finished: {quest is { isFinished: true }}");

                // ── Traversal state ──────────────────────────────────
                sb.AppendLine($"Current: {quest?.CurrentNodeTitle ?? ""} ({quest?.currentNodeId})");
                sb.AppendLine($"Last: {quest?.LastNodeTitle ?? ""} ({quest?.lastNodeId})");
                sb.AppendLine($"ReturnStack: {quest?.ReturnStack.Count ?? 0}");
                sb.AppendLine($"Entered: {quest?.EnteredNodes.Count ?? 0}");
                sb.AppendLine($"SequenceIndex: {quest?.SequenceIndex.Count ?? 0}");

                // ── Variables (mirrors HUD output) ───────────────────
                if (ctx != null && ctx.Variables.Count > 0)
                {
                    sb.AppendLine($"Vars:");
                    foreach (var kv in ctx.Variables)
                        sb.AppendLine($"{kv.Key} = {kv.Value}");
                }
                else
                {
                    sb.AppendLine($"Vars: ————————————");
                }

                sb.AppendLine();
            }

            debugSummary = sb.ToString();
        }

        // Context menu shortcut so a save ID can be regenerated directly from the inspector
        [ContextMenu("Generate Save Id")]
        public void GenerateSaveId()
        {
            saveId = Guid.NewGuid().ToString("D");
        }
    }
}