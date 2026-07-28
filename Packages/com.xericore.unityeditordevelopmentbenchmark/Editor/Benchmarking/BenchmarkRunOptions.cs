namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking
{
    /// <summary>
    /// How many times to repeat each benchmark category's timed operation during one
    /// <see cref="BenchmarkRunner"/> run. Passed to
    /// <see cref="BenchmarkRunner.StartBenchmark(BenchmarkRunOptions)"/>; any value below 1 is clamped up to 1
    /// (with a warning) rather than rejected outright.
    /// </summary>
    public sealed class BenchmarkRunOptions
    {
        /// <summary>How many times to enter and exit play mode. Defaults to 3.</summary>
        public int PlayModeSwitchCount { get; set; } = 3;

        /// <summary>
        /// How many times to force a full script recompilation (via
        /// <see cref="UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation()"/> with
        /// <see cref="UnityEditor.Compilation.RequestScriptCompilationOptions.CleanBuildCache"/> where available).
        /// Defaults to 3.
        /// </summary>
        public int CompilationRunCount { get; set; } = 3;

        /// <summary>
        /// How many times to force a full reimport of everything under the "Assets" folder (never "Packages").
        /// Defaults to 3.
        /// </summary>
        public int AssetImportRunCount { get; set; } = 3;

        /// <summary>
        /// How many times to bake lightmaps for the scene assigned to "Lightmap Benchmark Scene" in
        /// Project Settings &gt; Development Benchmark. Defaults to 2. Ignored (the category is skipped) if no
        /// scene is assigned.
        /// </summary>
        public int LightmapBakeRunCount { get; set; } = 2;

        /// <summary>
        /// How many times to build a player for the currently selected active build target
        /// (<see cref="UnityEditor.EditorUserBuildSettings.activeBuildTarget"/>), using the scenes currently
        /// enabled in Build Settings, into a temporary directory next to (not inside) "Assets". Defaults to 1.
        /// Ignored (the category is skipped) if no scenes are enabled in Build Settings.
        /// </summary>
        public int BuildRunCount { get; set; } = 1;
    }
}
