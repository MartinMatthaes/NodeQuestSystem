using System;
using UnityEngine;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Actions
{
    [Serializable]
    public class AddItemAction : QuestAction
    {
        [SerializeField] private string itemId;
        [SerializeField] private int amount = 1;

        public override void Execute(QuestContext ctx)
        {
            if (ctx == null) return;
            if (string.IsNullOrWhiteSpace(itemId)) return;

            ctx.AddItem(itemId, amount);
        }
    }
}