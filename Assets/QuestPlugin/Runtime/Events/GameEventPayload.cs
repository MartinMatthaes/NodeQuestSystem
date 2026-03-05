using System;
using UnityEngine.Serialization;

namespace QuestPlugin.Runtime.Events
{
    /// <summary>
    /// Data packet carried by a raised GameEvent. All fields are optional — populate
    /// only what is meaningful for a given event type. Defined as a struct because
    /// payloads are short-lived, passed by value, and carry no identity beyond their
    /// fields, making heap allocation unnecessary.
    /// </summary>
    [Serializable]
    public struct GameEventPayload
    {
        [FormerlySerializedAs("Amount")]   public int amount;    // Numeric quantity (e.g. damage dealt, items collected)
        [FormerlySerializedAs("TargetId")] public string targetId;  // Identifier of the entity the event acted upon
        [FormerlySerializedAs("ActorId")]  public string actorId;   // Identifier of the entity that triggered the event

        public GameEventPayload(int amount = 1, string targetId = null, string actorId = null)
        {
            this.amount = amount;
            this.targetId = targetId;
            this.actorId = actorId;
        }
    }
}