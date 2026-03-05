using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace QuestPlugin.Runtime.Core
{
    /// <summary>
    /// Runtime data container for a single quest execution instance.
    /// Holds all mutable state for a running quest: variables, counters, inventory,
    /// and any runtime references registered by the scene. Passed to every
    /// QuestCondition and QuestAction so they can read and write shared state
    /// without needing direct references to each other.
    /// </summary>
    public class QuestContext
    {
        /// <summary>The scene GameObject this quest is executing on behalf of.</summary>
        public GameObject Actor;

        // All runtime state is stored in a single dictionary keyed by namespaced strings.
        // Namespacing (e.g. "inv.", "counter.", "ref.") keeps different data categories
        // from colliding even though they share the same underlying dictionary.
        private readonly Dictionary<string, object> _vars = new();

        /// <summary>Read-only view of all stored variables, exposed for serialization and debugging.</summary>
        public IReadOnlyDictionary<string, object> Variables => _vars;

        /// <summary>The QuestRunner that owns and drives this context.</summary>
        public QuestRunner Runner { get; private set; }

        /// <summary>Index of this quest within the runner, used to scope inbox event lookups.</summary>
        public int QuestIndex { get; internal set; }

        /// <summary>
        /// Creates a new context bound to a scene actor. Runner may be null in
        /// unit test scenarios where no full QuestRunner is present.
        /// </summary>
        public QuestContext(GameObject actor, QuestRunner runner = null)
        {
            Actor  = actor;
            Runner = runner;
        }

        /// <summary>
        /// Returns the value of a variable cast to T, or <paramref name="fallback"/>
        /// if the key is missing or the stored value is not assignable to T.
        /// Never throws — callers can rely on the fallback for uninitialised variables.
        /// </summary>
        public T GetVar<T>(string key, T fallback = default)
        {
            if (_vars.TryGetValue(key, out var v) && v is T t) return t;
            return fallback;
        }

        /// <summary>Sets or overwrites a variable. Passing null is valid and stores null explicitly.</summary>
        public void SetVar(string key, object value)
        {
            _vars[key] = value;
        }

        /// <summary>
        /// Serializes all persistable variables to a flat list of typed string entries
        /// suitable for JSON serialization. Keys starting with "ref." are excluded
        /// because they hold scene Transform references which cannot be serialized.
        /// Floats use InvariantCulture to ensure consistent decimal formatting across locales.
        /// </summary>
        public List<VarEntry> ExportVars()
        {
            var list = new List<VarEntry>();

            foreach (var kv in _vars)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                if (kv.Value == null) continue;
                if (kv.Key.StartsWith("ref.")) continue;  // Transform refs are scene-bound, not serialisable

                if (kv.Value is int i)
                {
                    list.Add(new VarEntry { key = kv.Key, type = "int", value = i.ToString() });
                    continue;
                }

                if (kv.Value is float f)
                {
                    // InvariantCulture prevents locale-dependent decimal separators (e.g. "1,5" vs "1.5")
                    list.Add(new VarEntry { key = kv.Key, type = "float", value = f.ToString(CultureInfo.InvariantCulture) });
                    continue;
                }

                if (kv.Value is bool b)
                {
                    // Store bools as "1"/"0" for compact representation and easy cross-platform parsing
                    list.Add(new VarEntry { key = kv.Key, type = "bool", value = b ? "1" : "0" });
                    continue;
                }

                if (kv.Value is string s)
                {
                    list.Add(new VarEntry { key = kv.Key, type = "string", value = s });
                    continue;
                }

                // Fallback for any other stored type — ToString() gives a best-effort string representation
                list.Add(new VarEntry { key = kv.Key, type = "string", value = kv.Value.ToString() });
            }

            return list;
        }

        /// <summary>
        /// Deserialises a list of typed string entries back into the variable dictionary.
        /// Entries that fail to parse are silently skipped so a single corrupt entry
        /// does not prevent the rest of the save data from loading.
        /// </summary>
        public void ImportVars(List<VarEntry> vars)
        {
            if (vars == null) return;

            foreach (var v in vars)
            {
                if (v == null) continue;
                if (string.IsNullOrEmpty(v.key)) continue;

                object parsed = null;

                if (v.type == "int")
                {
                    if (int.TryParse(v.value, out int i)) parsed = i;
                }
                else if (v.type == "float")
                {
                    // InvariantCulture must match the format used during export
                    if (float.TryParse(v.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) parsed = f;
                }
                else if (v.type == "bool")
                {
                    // Accept both numeric ("1"/"0") and textual ("true"/"false") representations
                    if (v.value == "1")                          parsed = true;
                    else if (v.value == "0")                     parsed = false;
                    else if (bool.TryParse(v.value, out var b)) parsed = b;
                }
                else
                {
                    parsed = v.value ?? "";  // Default unknown types to string; empty string rather than null
                }

                if (parsed != null)
                    _vars[v.key] = parsed;
            }
        }

        // Namespace helpers — centralize the key prefix convention so it only
        // needs to change in one place if the naming scheme ever evolves
        private static string C(string key) => "counter." + key;
        private static string I(string key) => "inv."     + key;

        // Counter reads are private since external code uses AddCounter/SetCounter
        private int GetCounter(string key, int fallback = 0) => GetVar(C(key), fallback);

        /// <summary>Increments a named counter by delta and returns the new value.</summary>
        public int AddCounter(string key, int delta = 1)
        {
            var next = GetCounter(key) + delta;
            SetVar(C(key), next);
            return next;
        }

        /// <summary>Sets a named counter to an explicit absolute value.</summary>
        public void SetCounter(string key, int value) => SetVar(C(key), value);

        // Item reads are private since external code uses AddItem/TrySpendItem/HasItem
        private int GetItem(string itemId, int fallback = 0) => GetVar(I(itemId), fallback);

        /// <summary>Adds delta units of an item to inventory and returns the new total.</summary>
        public int AddItem(string itemId, int delta = 1)
        {
            var next = GetItem(itemId) + delta;
            SetVar(I(itemId), next);
            return next;
        }

        /// <summary>Removes delta units of an item from inventory and returns the new total.
        /// Does not enforce a floor — callers should check HasItem first if a negative count is undesirable.</summary>
        public int RemoveItem(string itemId, int delta = 1)
        {
            var next = GetItem(itemId) - delta;
            SetVar(I(itemId), next);
            return next;
        }

        /// <summary>Returns true if the inventory contains at least the requested amount.</summary>
        public bool HasItem(string itemId, int amount) => GetItem(itemId) >= amount;

        /// <summary>
        /// Attempts to deduct amount units from inventory. Returns true and applies
        /// the deduction if sufficient stock exists; returns false without modifying
        /// inventory if the current count is less than amount.
        /// </summary>
        public bool TrySpendItem(string itemId, int amount)
        {
            var have = GetItem(itemId);
            if (have < amount) return false;

            SetVar(I(itemId), have - amount);
            return true;
        }
    }
}