namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking.Categories
{
    /// <summary>
    /// What <see cref="IBenchmarkCategoryRunner.Tick"/> tells <see cref="BenchmarkRunner"/> to do next.
    /// </summary>
    public enum BenchmarkCategoryTickResult
    {
        /// <summary>Keep calling <see cref="IBenchmarkCategoryRunner.Tick"/> next update.</summary>
        InProgress,

        /// <summary>
        /// This category finished (successfully, or having logged its own warning about a partial failure that
        /// doesn't warrant aborting the whole run - e.g. a single failed player build). Move on to the next
        /// category.
        /// </summary>
        Completed,

        /// <summary>
        /// This category couldn't run at all (e.g. no scene/scenes configured for it) and logged its own warning
        /// explaining why. Move on to the next category; this category reports a duration of zero.
        /// </summary>
        Skipped,

        /// <summary>
        /// This category got stuck (a phase timed out) and logged its own warning explaining what it was waiting
        /// for. Abort the entire benchmark run.
        /// </summary>
        Failed
    }

    /// <summary>
    /// One category of the automated benchmark that <see cref="BenchmarkRunner"/> drives, one at a time, via
    /// <see cref="UnityEditor.EditorApplication.update"/>. Each implementation owns exactly one
    /// <see cref="BenchmarkCategory"/>: its own internal sub-state machine (for categories that need more than one
    /// step, e.g. "request" then "wait for it to finish"), its own iteration bookkeeping, and any category-specific
    /// setup/cleanup (temporary scenes, temporary build output directories, etc.).
    /// </summary>
    /// <remarks>
    /// Implementations must not hold any state in ordinary fields - a benchmark run spans domain reloads (entering
    /// play mode, forced recompilations), which destroy all such state. Every bit of state that needs to survive
    /// must instead be persisted via <see cref="UnityEditor.SessionState"/> (see
    /// <see cref="UnityEditorDevelopmentBenchmark.Editor.Util.PersistentRunCounter"/> for the shared count/iteration
    /// bookkeeping every implementation needs).
    /// </remarks>
    public interface IBenchmarkCategoryRunner
    {
        /// <summary>The category this instance measures.</summary>
        BenchmarkCategory Category { get; }

        /// <summary>
        /// Called exactly once, when this category becomes the active one: resets its iteration counter to zero
        /// and its internal sub-state to its first step.
        /// </summary>
        /// <param name="runCount">How many times this category's timed operation should repeat this run.</param>
        void Begin(int runCount);

        /// <summary>
        /// Called every <see cref="UnityEditor.EditorApplication.update"/> tick while this category is the active
        /// one, until it reports anything other than <see cref="BenchmarkCategoryTickResult.InProgress"/>.
        /// </summary>
        BenchmarkCategoryTickResult Tick(IBenchmarkRunnerContext context);

        /// <summary>
        /// Best-effort cleanup, called on every category (regardless of whether it's the one currently active)
        /// when a run stops before reaching its normal final state (<see cref="BenchmarkRunner.StopBenchmark"/>,
        /// or a category reporting <see cref="BenchmarkCategoryTickResult.Failed"/>). Must be a safe no-op if this
        /// category never started, or had already finished (and cleaned up after itself) normally.
        /// </summary>
        void Abort();
    }
}
