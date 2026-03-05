using System;
using UnityEngine;
using QuestPlugin.Runtime.Core;
using UnityEngine.Serialization;

namespace QuestPlugin.Runtime.Conditions
{
    /// <summary>
    /// Compares a named float variable in the quest context against a fixed threshold
    /// using a configurable operator. Equality and inequality comparisons use an epsilon
    /// tolerance rather than exact matching to avoid floating-point precision errors.
    /// </summary>
    [Serializable]
    public class FloatVarCompareCondition : QuestCondition
    {
        // [FormerlySerializedAs] attributes preserve data in existing assets
        // after the field names were lowercased to match C# naming conventions
        [FormerlySerializedAs("Key")]   public string key;
        [FormerlySerializedAs("Op")]    public CompareOp op = CompareOp.GreaterOrEqual;
        [FormerlySerializedAs("Value")] public float value = 1f;

        // Tolerance used for Equal/NotEqual only — irrelevant for ordered comparisons
        [FormerlySerializedAs("Epsilon")] public float epsilon = 0.0001f;

        public override bool Evaluate(QuestContext ctx)
        {
            if (ctx == null) return false;

            var a = ctx.GetVar<float>(key);
            var b = value;

            return op switch
            {
                CompareOp.Less           => a < b,
                CompareOp.LessOrEqual    => a <= b,
                // Abs difference against epsilon avoids false negatives from floating-point drift
                CompareOp.Equal          => Mathf.Abs(a - b) <= epsilon,
                CompareOp.NotEqual       => Mathf.Abs(a - b) >  epsilon,
                CompareOp.GreaterOrEqual => a >= b,
                CompareOp.Greater        => a > b,
                _                        => false
            };
        }

        public override string GetEditorSummary()
        {
            if (string.IsNullOrWhiteSpace(key))
                return "float var\nkey: <empty>";

            // Only show epsilon for equality checks where it affects the result
            if (op is CompareOp.Equal or CompareOp.NotEqual)
            {
                return $"key: {key}\n" +
                       $"op: {op}\n"  +
                       $"value: {value}\n" +
                       $"eps: {epsilon}";
            }

            return $"key: {key}\n" +
                   $"op: {op}\n"  +
                   $"value: {value}";
        }
    }
}