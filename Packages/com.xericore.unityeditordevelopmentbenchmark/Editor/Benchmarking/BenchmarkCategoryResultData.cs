using System;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking
{
    /// <summary>
    /// A single row for <see cref="BenchmarkRunnerEditorWindow"/>'s results table: either one
    /// <see cref="BenchmarkCategory"/>'s figures from the last (or currently in-progress) benchmark run, or the
    /// synthesized "Total" row summing all of them.
    /// </summary>
    public class BenchmarkCategoryResultData
    {
        public string Name { get; }

        /// <summary>
        /// The duration to display/color-code per category, and the basis for the "Ratio (Total)" column: the
        /// average duration of one occurrence of this category's timed operation (see
        /// <see cref="BenchmarkCategoryTimeTracker.GetAverage"/>), not the sum across every occurrence -
        /// categories run a different number of times each (e.g. 3 play mode switches vs 1 build by default),
        /// which would otherwise make their raw totals misleading to compare directly. For the synthesized
        /// "Total" row, this is simply the overall benchmark total (an "average" isn't a meaningful concept for
        /// that row).
        /// </summary>
        public TimeSpan AverageDuration { get; }

        /// <summary>
        /// Whether this row is the synthesized total row rather than an actual <see cref="BenchmarkCategory"/>,
        /// so it can be styled differently (bold, uncolored) and excluded from the min/max range used to
        /// color-code the other rows relative to each other.
        /// </summary>
        public bool IsTotal { get; }

        public BenchmarkCategoryResultData(string name, TimeSpan averageDuration, bool isTotal)
        {
            Name = name;
            AverageDuration = averageDuration;
            IsTotal = isTotal;
        }
    }
}
