using System;
using QuestPlugin.Runtime.Core;
using UnityEngine.Serialization;

namespace QuestPlugin.Runtime.Conditions
{
    /// <summary>
    /// The set of relational operators shared by integer and float comparison conditions.
    /// Defined here alongside IntVarCompareCondition as it was the first consumer;
    /// FloatVarCompareCondition reuses the same enum.
    /// </summary>
    public enum CompareOp
    {
        Less,
        LessOrEqual,
        Equal,
        NotEqual,
        GreaterOrEqual,
        Greater
    }

    /// <summary>
    /// Compares a named integer variable in the quest context against a fixed threshold
    /// using a configurable operator. Unlike FloatVarCompareCondition, integer equality
    /// uses exact matching since integers are not subject to floating-point drift.
    /// </summary>
    [Serializable]
    public class IntVarCompareCondition : QuestCondition
    {
        // [FormerlySerializedAs] attributes preserve data in existing assets
        // after the field names were lowercased to match C# naming conventions
        [FormerlySerializedAs("Key")]   public string key;
        [FormerlySerializedAs("Op")]    public CompareOp op = CompareOp.GreaterOrEqual;
        [FormerlySerializedAs("Value")] public int value = 1;

        public override bool Evaluate(QuestContext ctx)
        {
            if (ctx == null) return false;

            int a = ctx.GetVar<int>(key);
            int b = value;

            return op switch
            {
                CompareOp.Less           => a < b,
                CompareOp.LessOrEqual    => a <= b,
                CompareOp.Equal          => a == b,  // Exact match is safe for integers
                CompareOp.NotEqual       => a != b,
                CompareOp.GreaterOrEqual => a >= b,
                CompareOp.Greater        => a > b,
                _                        => false
            };
        }

        public override string GetEditorSummary()
        {
            return $"Key: {key}\nOperation: {op}\nValue: {value}";
        }
    }
}