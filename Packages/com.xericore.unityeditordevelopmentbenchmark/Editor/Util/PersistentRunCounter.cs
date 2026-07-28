using UnityEditor;

namespace UnityEditorDevelopmentBenchmark.Editor.Util
{
    /// <summary>
    /// A <see cref="SessionState"/>-backed (count, iteration) pair: how many times a benchmark category's timed
    /// operation should repeat during one run, and how many of those repetitions have completed so far. Backed
    /// by <see cref="SessionState"/> rather than fields since a benchmark run spans domain reloads (entering play
    /// mode, forced recompilations) which would otherwise wipe any in-memory counter. Extracted out of individual
    /// benchmark categories since every one of them needs exactly these same two values.
    /// </summary>
    public sealed class PersistentRunCounter
    {
        private readonly string _countKey;
        private readonly string _iterationKey;
        private readonly int _defaultCount;

        /// <param name="keyPrefix">
        /// Unique prefix for this counter's two <see cref="SessionState"/> keys (e.g. the owning category
        /// runner's fully qualified type name), so multiple counters don't collide with each other.
        /// </param>
        /// <param name="defaultCount">
        /// Fallback returned by <see cref="Count"/> before it's ever explicitly set. Irrelevant in practice, since
        /// callers are expected to set <see cref="Count"/> themselves before reading it (e.g. from
        /// <c>Begin(runCount)</c>).
        /// </param>
        public PersistentRunCounter(string keyPrefix, int defaultCount)
        {
            _countKey = keyPrefix + ".Count";
            _iterationKey = keyPrefix + ".Iteration";
            _defaultCount = defaultCount;
        }

        /// <summary>How many times this run's operation should repeat in total.</summary>
        public int Count
        {
            get => SessionState.GetInt(_countKey, _defaultCount);
            set => SessionState.SetInt(_countKey, value);
        }

        /// <summary>How many repetitions have completed so far this run.</summary>
        public int Iteration
        {
            get => SessionState.GetInt(_iterationKey, 0);
            set => SessionState.SetInt(_iterationKey, value);
        }
    }
}
