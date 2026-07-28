namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking.Categories
{
    /// <summary>
    /// Shared, orchestrator-owned services every <see cref="IBenchmarkCategoryRunner"/> needs but that don't
    /// belong to any single category: a single phase-timeout clock, and the short "settle" delay used by more
    /// than one category.
    /// </summary>
    /// <remarks>
    /// There is exactly one phase timer for the entire benchmark run (owned centrally by
    /// <see cref="BenchmarkRunner"/>), shared by every category - not one per category. It's reset both when the
    /// orchestrator moves on to a new category, and whenever a category moves between its own internal sub-steps,
    /// exactly like a single top-level state machine would; splitting the state machine across category classes
    /// doesn't change that there's only one clock.
    /// </remarks>
    public interface IBenchmarkRunnerContext
    {
        /// <summary>
        /// Seconds elapsed since the last call to <see cref="ResetPhaseTimer"/>. Categories that need to wait out
        /// a fixed delay (rather than detect getting stuck) compare this against
        /// <see cref="PreparationDelaySeconds"/> themselves.
        /// </summary>
        double ElapsedPhaseTime { get; }

        /// <summary>Whether more time than the configured max phase duration has elapsed since the last call to
        /// <see cref="ResetPhaseTimer"/>. Categories check this while waiting for something (compilation to
        /// start/finish, play mode to enter/exit, etc.) that could get stuck.</summary>
        bool HasPhaseTimedOut();

        /// <summary>
        /// Marks "now" as the start of a new phase for <see cref="HasPhaseTimedOut"/>/<see cref="ElapsedPhaseTime"/>
        /// purposes. Category runners must call this every time they transition their own internal sub-state,
        /// exactly as the orchestrator does when moving between categories.
        /// </summary>
        void ResetPhaseTimer();

        /// <summary>
        /// Short settle delay shared by categories that need to wait a moment for external tooling/Unity itself
        /// before proceeding (e.g. Compilation waiting before forcing the next recompile, so other tools reading
        /// the Bee profiler trace from the previous one get a chance to finish; PlayModeSwitch waiting before
        /// entering play mode).
        /// </summary>
        float PreparationDelaySeconds { get; }
    }
}
