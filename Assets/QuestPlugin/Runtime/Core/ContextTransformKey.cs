using UnityEngine;

namespace QuestPlugin.Runtime.Core
{
    /// <summary>
    /// A ScriptableObject that acts as a typed dictionary key for looking up
    /// Transform references in a QuestContext. Using an asset reference instead
    /// of a plain string prevents typos and makes all usages refactor-safe —
    /// renaming the id updates every node that references this asset automatically.
    /// </summary>
    [CreateAssetMenu(menuName = "Quest/Transform Key")]
    public class ContextTransformKey : ScriptableObject
    {
        [Tooltip("Unique id used as the dictionary key when registering and retrieving this transform in QuestContext.")]
        public string id = "ref.spawnPoint";  // Convention: "ref." prefix distinguishes transform keys from value variables
    }
}