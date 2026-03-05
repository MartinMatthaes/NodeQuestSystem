using System;
using UnityEngine;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Actions
{
    /// <summary>
    /// Attempts to deduct a quantity of an item from the player's inventory.
    /// If the spend fails (insufficient quantity), an optional failure event is
    /// raised so other systems can respond — e.g. playing a "not enough items"
    /// UI sound or redirecting the quest graph to an alternate branch.
    /// </summary>
    [Serializable]
    public class SpendItemAction : QuestAction
    {
        [SerializeField] private string itemId;       // Identifier of the item to deduct
        [SerializeField] private int amount = 1;      // Quantity to spend per execution
        [SerializeField] private string failEvent;    // Event name raised on failure; leave empty to fail silently

        public override void Execute(QuestContext ctx)
        {
            if (ctx == null) return;
            if (string.IsNullOrWhiteSpace(itemId)) return;

            // TrySpendItem returns true if the deduction succeeded; nothing more to do in that case
            if (ctx.TrySpendItem(itemId, amount)) return;

            // Spend failed — notify listeners if a failure event has been configured
            if (!string.IsNullOrWhiteSpace(failEvent))
                QuestEvents.Raise(failEvent);
        }
    }
}