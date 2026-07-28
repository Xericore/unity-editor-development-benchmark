using System;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking
{
    /// <summary>
    /// A single row for <see cref="BenchmarkRunnerEditorWindow"/>'s results table: either one
    /// <see cref="BenchmarkCategory"/>'s duration from the last (or currently in-progress) benchmark run, or the
    /// synthesized "Total" row summing all of them.
    /// </summary>
    public class BenchmarkCategoryResultData
    {
        public string Name { get; }
        public TimeSpan Duration { get; }

        /// <summary>
        /// Whether this row is the synthesized total row rather than an actual <see cref="BenchmarkCategory"/>,
        /// so it can be styled differently (bold, uncolored) and excluded from the min/max range used to
        /// color-code the other rows relative to each other.
        /// </summary>
        public bool IsTotal { get; }

        public BenchmarkCategoryResultData(string name, TimeSpan duration, bool isTotal)
        {
            Name = name;
            Duration = duration;
            IsTotal = isTotal;
        }
    }
}
