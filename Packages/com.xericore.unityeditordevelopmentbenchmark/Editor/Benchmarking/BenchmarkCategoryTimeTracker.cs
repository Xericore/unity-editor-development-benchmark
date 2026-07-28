using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

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

            AddDuration(category, elapsed);

            return elapsed;
        }

        /// <summary>
        /// Adds <paramref name="duration"/> directly to <paramref name="category"/>'s running total (and counts it
        /// as one more sample towards <see cref="GetAverage"/>), for categories whose duration is measured
        /// externally (e.g. <see cref="UnityEditorDevelopmentBenchmark.Editor.Util.AssemblyReloadTimer"/>'s
        /// reconstructed domain reload duration) rather than via a matching <see cref="Start"/>/<see cref="Stop"/> pair.
        /// </summary>
        public static void AddDuration(BenchmarkCategory category, TimeSpan duration)
        {
            var newTotal = GetTotal(category) + duration;
            SessionState.SetString(TotalKey(category), newTotal.TotalSeconds.ToString(CultureInfo.InvariantCulture));

            SessionState.SetInt(SampleCountKey(category), GetSampleCount(category) + 1);
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
        /// How many spans have been recorded for <paramref name="category"/> (via <see cref="Stop"/> or
        /// <see cref="AddDuration"/>) since it was last reset - i.e. how many times this category's timed
        /// operation actually ran this benchmark run.
        /// </summary>
        public static int GetSampleCount(BenchmarkCategory category)
        {
            return SessionState.GetInt(SampleCountKey(category), 0);
        }

        /// <summary>
        /// <see cref="GetTotal"/> divided by <see cref="GetSampleCount"/> - the average duration of one occurrence
        /// of <paramref name="category"/>'s timed operation this run (e.g. one script recompilation, one play
        /// mode switch), rather than the sum across every occurrence. This is what should be displayed/compared
        /// per category, since categories run a different number of times each (e.g. 3 play mode switches vs 1
        /// build by default), which would otherwise make their raw totals misleading to compare directly.
        /// <see cref="TimeSpan.Zero"/> if the category never ran.
        /// </summary>
        public static TimeSpan GetAverage(BenchmarkCategory category)
        {
            var sampleCount = GetSampleCount(category);
            return sampleCount > 0 ? TimeSpan.FromTicks(GetTotal(category).Ticks / sampleCount) : TimeSpan.Zero;
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
        /// Running averages for every <see cref="BenchmarkCategory"/> (see <see cref="GetAverage"/>), including
        /// categories that are still stubs and therefore always report <see cref="TimeSpan.Zero"/>.
        /// </summary>
        public static IReadOnlyDictionary<BenchmarkCategory, TimeSpan> GetAllAverages()
        {
            var averages = new Dictionary<BenchmarkCategory, TimeSpan>();

            foreach (BenchmarkCategory category in Enum.GetValues(typeof(BenchmarkCategory)))
            {
                averages[category] = GetAverage(category);
            }

            return averages;
        }

        /// <summary>
        /// Sum of <see cref="GetTotal"/> across every <see cref="BenchmarkCategory"/>. This is the authoritative
        /// total duration for a benchmark run; callers must not derive it independently (e.g. from wall-clock time
        /// between start and end), since that would also include untracked phases (such as waiting for
        /// compilation before the first category starts) and silently diverge from the sum of the categories.
        /// </summary>
        public static TimeSpan GetTotalDurationFromAllCategories()
        {
            var total = TimeSpan.Zero;

            foreach (var categoryTotal in GetAllTotals().Values)
            {
                total += categoryTotal;
            }

            return total;
        }

        /// <summary>
        /// Clears the recorded total (and any in-progress start time) for <paramref name="category"/>.
        /// </summary>
        public static void Reset(BenchmarkCategory category)
        {
            SessionState.EraseString(StartTimeKey(category));
            SessionState.EraseString(TotalKey(category));

            // SessionState has no EraseInt; explicitly setting back to 0 has the same effect, since GetInt already
            // falls back to 0 for a key that was never set.
            SessionState.SetInt(SampleCountKey(category), 0);
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

        /// <summary>
        /// Lerps from green (shortest of the given range) to red (longest), so category durations can be
        /// compared visually relative to each other rather than against a fixed absolute scale. Shared by
        /// <see cref="BenchmarkRunner"/>'s console log breakdown and <see cref="BenchmarkRunnerEditorWindow"/>'s
        /// table, so both color-code categories the same way.
        /// </summary>
        public static Color GetDurationColor(double seconds, double minSeconds, double maxSeconds)
        {
            if (Mathf.Approximately((float) minSeconds, (float) maxSeconds))
            {
                return Color.green;
            }

            var t = (float) ((seconds - minSeconds) / (maxSeconds - minSeconds));
            return Color.Lerp(Color.green, Color.red, t);
        }

        private static string StartTimeKey(BenchmarkCategory category)
        {
            return _keyPrefix + category + ".StartTime";
        }

        private static string TotalKey(BenchmarkCategory category)
        {
            return _keyPrefix + category + ".Total";
        }

        private static string SampleCountKey(BenchmarkCategory category)
        {
            return _keyPrefix + category + ".SampleCount";
        }
    }
}
