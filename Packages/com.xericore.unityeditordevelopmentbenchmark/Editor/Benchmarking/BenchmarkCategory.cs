using UnityEditorDevelopmentBenchmark.Editor.UserWaitTimeTrackers;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking
{
    /// <summary>
    /// Categories of editor wait time that <see cref="BenchmarkRunner"/> can measure, mirroring the categories
    /// already tracked live by the <see cref="UserWaitTimeTrackerBase"/> subclasses in this namespace.
    /// </summary>
    /// <remarks>
    /// <see cref="PlayModeSwitch"/>, <see cref="Compilation"/> and <see cref="AssetImport"/> are currently driven
    /// by <see cref="BenchmarkRunner"/>. The remaining values are stubs for categories the benchmark does not
    /// yet exercise.
    /// </remarks>
    public enum BenchmarkCategory
    {
        /// <summary>
        /// Time spent entering and exiting play mode. Implemented, driven by <see cref="BenchmarkRunner"/>.
        /// </summary>
        PlayModeSwitch,

        /// <summary>
        /// Time spent forcing full script recompilations. Implemented, driven by <see cref="BenchmarkRunner"/>.
        /// </summary>
        Compilation,

        /// <summary>
        /// Stub. Not yet driven by <see cref="BenchmarkRunner"/>.
        /// </summary>
        DomainReload,

        /// <summary>
        /// Time spent forcing full reimports of everything under the "Assets" folder (never "Packages").
        /// Implemented, driven by <see cref="BenchmarkRunner"/>.
        /// </summary>
        AssetImport,

        /// <summary>
        /// Stub. Not yet driven by <see cref="BenchmarkRunner"/>.
        /// </summary>
        Build,

        /// <summary>
        /// Stub. Not yet driven by <see cref="BenchmarkRunner"/>.
        /// </summary>
        EditorStartup,

        /// <summary>
        /// Time spent baking lightmaps for the scene assigned to "Lightmap Benchmark Scene" in
        /// Project Settings &gt; Development Benchmark. Implemented, driven by <see cref="BenchmarkRunner"/>.
        /// Skipped (and reported as zero) if no scene is assigned.
        /// </summary>
        LightmapBaking
    }
}
