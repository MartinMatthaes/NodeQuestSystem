using System;
using UnityEngine;
using QuestPlugin.Runtime.Core;

namespace QuestPlugin.Runtime.Conditions
{
    /// <summary>
    /// A condition that returns false for a set duration, then true once the time
    /// has elapsed. The timer is self-initializing — the first evaluation records
    /// the start time in the quest context, and subsequent evaluations check against it.
    /// Once the timer fires, it resets so it can be triggered again if re-evaluated.
    /// </summary>
    [Serializable]
    public class TimerCondition : QuestCondition
    {
        public float durationSeconds = 0.1f;  // How long to wait before returning true
        public string timerKey = "default";   // Unique key per timer; change if multiple timers run concurrently

        // Namespaced prefix prevents collisions with other float variables in the same context
        private string StartKey => "timer.start." + timerKey;

        public override bool Evaluate(QuestContext ctx)
        {
            if (ctx == null) return false;

            var dur = Mathf.Max(0f, durationSeconds);  // Guard against negative durations

            // -1 is used as a sentinel to indicate the timer has not been started yet
            var start = ctx.GetVar(StartKey, -1f);

            if (start < 0f)
            {
                // First evaluation — record the current time and return false to begin waiting
                ctx.SetVar(StartKey, Time.time);
                return false;
            }

            if (!(Time.time - start >= dur)) return false;

            // Duration elapsed — reset the timer so it can fire again if re-evaluated
            ctx.SetVar(StartKey, -1f);
            return true;
        }

        public override string GetEditorSummary()
        {
            return string.IsNullOrWhiteSpace(timerKey)
                ? $"Timer\nDuration: {durationSeconds:0.###}s"
                : $"Timer: {timerKey}\nDuration: {durationSeconds:0.###}s";
        }
    }
}