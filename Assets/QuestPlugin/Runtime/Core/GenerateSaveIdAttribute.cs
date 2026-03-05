using UnityEngine;

namespace QuestPlugin.Runtime.Core
{
    /// <summary>
    /// Marker attribute that tells <see cref="QuestPlugin.Editor.Core.GenerateSaveIdDrawer"/>
    /// to augment the field's inspector with a right-click context menu for generating
    /// a UUID, resetting to default, or clearing the value.
    /// The attribute itself carries no runtime behaviour — all logic lives in the drawer.
    /// </summary>
    public class GenerateSaveIdAttribute : PropertyAttribute
    {
    }
}