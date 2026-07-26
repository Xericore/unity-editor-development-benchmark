using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking
{
    /// <summary>
    /// Accumulates elapsed time per <see cref="BenchmarkCategory"/> during a single <see cref="BenchmarkRunner"/>
    /// run. Kept separate from <see cref="BenchmarkRunner"/> so the two concerns (driving the benchmark state
    /// machine vs. recording its timings) don't get tangled together.
    /// </summary>
    /// <remarks>
    /// Backed by <see cref="SessionState"/> rather than fields, since a benchmark run spans at least one domain
    /// reload (triggered by entering play mode) which would otherwise wipe any in-memory accumulator.
    /// </remarks>
    public static class BenchmarkCategoryTimeTracker
    {
        private const string _keyPrefix = "UnityEditorDevelopmentBenchmark.BenchmarkCategoryTimeTracker.";

        /// <summary>
        /// Marks the start of a timed span for <paramref name="category"/>. Call <see cref="Stop"/> with a
        /// matching call to add the elapsed time to that category's running total.
        /// </summary>
        public static void Start(BenchmarkCategory category)
        {
            SessionState.SetString(StartTimeKey(category),
                EditorApplication.timeSinceStartup.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Ends a timed span started with <see cref="Start"/>, adds the elapsed time to
        /// <paramref name="category"/>'s running total, and returns just the elapsed time for this span.
        /// </summary>
        public static TimeSpan Stop(BenchmarkCategory category)
        {
            var startTime = double.Parse(SessionState.GetString(StartTimeKey(category), "0"),
                CultureInfo.InvariantCulture);
            var elapsed = TimeSpan.FromSeconds(EditorApplication.timeSinceStartup - startTime);

            var newTotal = GetTotal(category) + elapsed;
            SessionState.SetString(TotalKey(category), newTotal.TotalSeconds.ToString(CultureInfo.InvariantCulture));

            return elapsed;
        }

        /// <summary>
        /// Running total of all elapsed spans recorded for <paramref name="category"/> since it was last reset.
        /// </summary>
        public static TimeSpan GetTotal(BenchmarkCategory category)
        {
            var totalSeconds = double.Parse(SessionState.GetString(TotalKey(category), "0"),
                CultureInfo.InvariantCulture);
            return TimeSpan.FromSeconds(totalSeconds);
        }

        /// <summary>
        /// Running totals for every <see cref="BenchmarkCategory"/>, including categories that are still stubs
        /// and therefore always report <see cref="TimeSpan.Zero"/>.
        /// </summary>
        public static IReadOnlyDictionary<BenchmarkCategory, TimeSpan> GetAllTotals()
        {
            var totals = new Dictionary<BenchmarkCategory, TimeSpan>();

            foreach (BenchmarkCategory category in Enum.GetValues(typeof(BenchmarkCategory)))
            {
                totals[category] = GetTotal(category);
            }

            return totals;
        }

        /// <summary>
        /// Clears the recorded total (and any in-progress start time) for <paramref name="category"/>.
        /// </summary>
        public static void Reset(BenchmarkCategory category)
        {
            SessionState.EraseString(StartTimeKey(category));
            SessionState.EraseString(TotalKey(category));
        }

        /// <summary>
        /// Clears the recorded totals for every <see cref="BenchmarkCategory"/>.
        /// </summary>
        public static void ResetAll()
        {
            foreach (BenchmarkCategory category in Enum.GetValues(typeof(BenchmarkCategory)))
            {
                Reset(category);
            }
        }

        private static string StartTimeKey(BenchmarkCategory category)
        {
            return _keyPrefix + category + ".StartTime";
        }

        private static string TotalKey(BenchmarkCategory category)
        {
            return _keyPrefix + category + ".Total";
        }
    }
}
