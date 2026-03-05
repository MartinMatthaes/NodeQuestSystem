using QuestPlugin.Runtime.Core;
using UnityEngine;

namespace QuestPlugin.Runtime.Actions
{
    /// <summary>
    /// Instantiates a prefab one or more times when executed, with optional
    /// spawn point and parent transform looked up from the quest context.
    /// Falls back to a fixed world position if no spawn point is provided,
    /// making it usable both with and without runtime-registered transforms.
    /// </summary>
    [System.Serializable]
    public class SpawnPrefabAction : QuestAction
    {
        public GameObject prefab;

        public ContextTransformKey spawnPointKey;  // Context key for spawn position/rotation; optional
        public ContextTransformKey parentKey;      // Context key for the instantiated object's parent; optional

        public Vector3 fallbackPosition = Vector3.zero;  // World position used when no spawn point is registered
        public int amount = 1;                           // Number of instances to spawn per execution

        public override void Execute(QuestContext ctx)
        {
            if (prefab == null) return;

            Transform spawnPoint = null;
            Transform parent     = null;

            // Only look up transforms if a key has been assigned; avoids a context lookup with a null id
            if (spawnPointKey != null) spawnPoint = ctx.GetVar<Transform>(spawnPointKey.id);
            if (parentKey     != null) parent     = ctx.GetVar<Transform>(parentKey.id);

            // Inherit the spawn point's full transform so spawned objects respect level placement rotation
            var    pos = spawnPoint != null ? spawnPoint.position : fallbackPosition;
            var rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            // Clamp to at least 1 to guard against a designer accidentally setting amount to 0 or negative
            var n = Mathf.Max(1, amount);
            for (var i = 0; i < n; i++)
                Object.Instantiate(prefab, pos, rot, parent);
        }
    }
}